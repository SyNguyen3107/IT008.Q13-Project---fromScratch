using EasyFlips.Interfaces;
using EasyFlips.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace EasyFlips.Services
{
    public enum ReviewOutcome
    {
        Again,
        Hard,
        Good,
        Easy
    }

    public class StudyService
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository;
        private readonly IAuthService _authService; 

        public StudyService(ICardRepository cardRepository, IDeckRepository deckRepository, IAuthService authService)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository;
            _authService = authService;
        }

        public async Task<Card?> GetNextCardToReviewAsync(string deckId)
        {

            var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            Debug.WriteLine($"[StudyService] Total cards in deck {deckId}: {allCards.Count}");

            var dueCard = allCards
                .Where(c =>
                {
                    // CASE 1: Thẻ mới (New)
                    if (c.Progress == null) return true;

                    // CASE 2: Thẻ đang học (Learn: 0 < Interval < 1)
                    if (c.Progress.Interval > 0 && c.Progress.Interval < 1) return true;

                    // CASE 3: Thẻ đến hạn (Review: Interval >= 1)
                    // Thêm buffer 1 phút để tránh lệch giờ
                    if (c.Progress.Interval >= 1 && c.Progress.DueDate <= now.AddMinutes(1)) return true;

                    return false;
                })
                .OrderBy(c =>
                {
                    // Ưu tiên: New (0) -> Learn (1) -> Review (2)
                    if (c.Progress == null) return 0;
                    if (c.Progress.Interval > 0 && c.Progress.Interval < 1) return 1;
                    return 2;
                })
                .ThenBy(c => c.Progress == null ? DateTime.MinValue : c.Progress.DueDate)
                .FirstOrDefault();

            return dueCard;
        }

        // --- 2. LOGIC TÍNH TOÁN THỐNG KÊ ---
        public async Task<DeckStats> GetDeckStatsAsync(string deckId)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            var newCount = cards.Count(c => c.Progress == null);

            var learningCount = cards.Count(c => c.Progress != null &&
                                                 c.Progress.Interval > 0 &&
                                                 c.Progress.Interval < 1);

            var reviewCount = cards.Count(c => c.Progress != null &&
                                               c.Progress.Interval >= 1 &&
                                               c.Progress.DueDate <= now);

            return new DeckStats
            {
                DeckName = deck?.Name ?? "Unknown Deck",
                NewCount = newCount,
                LearningCount = learningCount,
                ReviewCount = reviewCount
            };
        }

        // --- 3. THUẬT TOÁN SM-2 ---
        public async Task ProcessReviewAsync(Card card, ReviewOutcome outcome)
        {
            if (card == null) return;

            // Nếu thẻ chưa có Progress (lần đầu học), tạo mới
            if (card.Progress == null)
            {
                card.Progress = new CardProgress();
                card.Progress.CardId = card.Id;

                // [FIX CRITICAL ERROR]: Gán UserId bắt buộc cho Database Supabase
                if (_authService.IsLoggedIn)
                {
                    card.Progress.UserId = _authService.CurrentUserId;
                }
                else
                {
                    // Nếu app cho phép học offline/guest, cần xử lý logic ID ảo hoặc throw error
                    // Hiện tại assume user đã login
                    throw new Exception("You must be logged in to save your learning progress.");
                }

                // Ngắt tham chiếu vòng để tránh lỗi EF Core insert lại Card
                card.Progress.Card = null;
            }

            var p = card.Progress;

            // Logic SM-2 cơ bản
            switch (outcome)
            {
                case ReviewOutcome.Again:
                    p.Interval = 0.01; // Reset về 0 (hoặc rất nhỏ)
                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.2);
                    p.DueDate = DateTime.Now; // Học lại ngay
                    p.Repetitions = 0; // Reset số lần lặp
                    break;

                case ReviewOutcome.Hard:
                    if (p.Interval < 0.01) p.Interval = 0.01;
                    else if (p.Interval < 1) p.Interval = Math.Min(p.Interval * 1.5, 0.9);
                    else p.Interval = 0.5; // Hard thường giảm interval hoặc giữ nguyên ngắn

                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.15);
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;

                case ReviewOutcome.Good:
                    if (p.Interval < 1) p.Interval = 1.0; // Tốt nghiệp giai đoạn học
                    else p.Interval = p.Interval * p.EaseFactor;

                    p.Repetitions++;
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;

                case ReviewOutcome.Easy:
                    if (p.Interval < 1) p.Interval = 4.0; // Nhảy cóc
                    else p.Interval = p.Interval * p.EaseFactor * 1.3;

                    p.EaseFactor += 0.15;
                    p.Repetitions++;
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;
            }

            p.LastReviewDate = DateTime.Now;

            // Lưu xuống DB
            await _cardRepository.UpdateAsync(card);
        }
    }
}