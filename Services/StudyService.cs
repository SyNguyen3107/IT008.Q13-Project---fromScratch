using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.Diagnostics; // Để in log debug

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

        public StudyService(ICardRepository cardRepository, IDeckRepository deckRepository)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository;
        }

        // --- 1. LOGIC LẤY THẺ CẦN HỌC ---
        public async Task<Card?> GetNextCardToReviewAsync(string deckId)
        {
            var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            Debug.WriteLine($"[StudyService] Total cards in deck {deckId}: {allCards.Count}");

            var dueCard = allCards
                .Where(c =>
                {
                    // CASE 1: Thẻ mới tinh (Chưa xem bao giờ, Progress = null) -> LẤY NGAY
                    if (c.Progress == null) return true;

                    // CASE 2: Thẻ đang học (0 < Interval < 1) -> LẤY NGAY
                    // Bao gồm thẻ Again (Interval = 0.01) và Hard (Interval < 1)
                    if (c.Progress.Interval > 0 && c.Progress.Interval < 1) return true;

                    // CASE 3: Thẻ ôn tập (Interval >= 1) đã đến hạn -> LẤY
                    if (c.Progress.Interval >= 1 && c.Progress.DueDate <= now.AddMinutes(1)) return true;

                    return false;
                })
                // Sắp xếp ưu tiên:
                // 1. Thẻ chưa có Progress (New) -> Rank 0
                // 2. Thẻ Learn (0 < Interval < 1) -> Rank 1
                // 3. Thẻ Review (Interval >= 1) -> Rank 2
                .OrderBy(c =>
                {
                    if (c.Progress == null) return 0;
                    if (c.Progress.Interval > 0 && c.Progress.Interval < 1) return 1;
                    return 2;
                })
                // Nếu cùng nhóm ưu tiên, lấy thẻ có DueDate cũ nhất trước
                .ThenBy(c => c.Progress == null ? DateTime.MinValue : c.Progress.DueDate)
                .FirstOrDefault();

            if (dueCard != null)
            {
                Debug.WriteLine($"[StudyService] Found card: {dueCard.FrontText} (ID: {dueCard.Id})");
            }
            else
            {
                Debug.WriteLine("[StudyService] No due card found.");
            }

            return dueCard;
        }

        // --- 2. LOGIC TÍNH TOÁN THỐNG KÊ ---
        public async Task<DeckStats> GetDeckStatsAsync(string deckId)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            // Đếm số thẻ New (CHƯA CÓ PROGRESS - chưa xem bao giờ)
            var newCount = cards.Count(c => c.Progress == null);

            // Đếm số thẻ Learn (ĐÃ XEM nhưng chưa hoàn thành: 0 < Interval < 1)
            // Chỉ đếm card có Interval > 0 (đã học) và < 1 (chưa hoàn thành)
            var learningCount = cards.Count(c => c.Progress != null &&
                                                 c.Progress.Interval > 0 &&
                                                 c.Progress.Interval < 1);

            // Đếm số thẻ Due/Review (Đã hoàn thành: Interval >= 1 VÀ đã đến hạn)
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

            if (card.Progress == null)
            {
                card.Progress = new CardProgress();
                card.Progress.CardId = card.Id; // gán khóa ngoại để EF khỏi lỗi
            }
            var p = card.Progress;

            switch (outcome)
            {
                case ReviewOutcome.Again:
                    p.Interval = 0.01;
                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.2);
                    p.DueDate = DateTime.Now;
                    break;
                case ReviewOutcome.Hard:
                    if (p.Interval < 0.01) p.Interval = 0.01;
                    else if (p.Interval < 1) p.Interval = Math.Min(p.Interval * 1.5, 0.9);
                    else p.Interval = 0.5;
                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.15);
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;
                case ReviewOutcome.Good:
                    if (p.Interval < 1) p.Interval = 1.0; else p.Interval = p.Interval * p.EaseFactor;
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;
                case ReviewOutcome.Easy:
                    if (p.Interval < 1) p.Interval = 4.0; else p.Interval = p.Interval * p.EaseFactor * 1.3;
                    p.EaseFactor += 0.15;
                    p.DueDate = DateTime.Now.AddDays(p.Interval);
                    break;
            }
            await _cardRepository.UpdateAsync(card);
        }
    }
}