using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics; // Để in log debug

namespace IT008.Q13_Project___fromScratch.Services
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

        // --- 1. LOGIC LẤY THẺ CẦN HỌC (ĐÃ SỬA LỖI TRIỆT ĐỂ) ---
        public async Task<Card?> GetNextCardToReviewAsync(int deckId)
        {
            var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            // In log để kiểm tra (xem trong cửa sổ Output của Visual Studio)
            Debug.WriteLine($"[StudyService] Total cards in deck {deckId}: {allCards.Count}");

            var dueCard = allCards
                .Where(c =>
                {
                    // CASE 1: Thẻ mới tinh (Chưa học lần nào) -> LẤY NGAY
                    if (c.Progress == null) return true;

                    // CASE 2: Thẻ đang học hoặc bị quên (Interval ~ 0) -> LẤY NGAY
                    // (Dùng < 0.01 để tránh lỗi làm tròn số thực)
                    if (c.Progress.Interval < 0.01) return true;

                    // CASE 3: Thẻ ôn tập đã đến hạn (DueDate <= Now) -> LẤY
                    // Thêm 1 phút dung sai để tránh lỗi lệch giây
                    if (c.Progress.DueDate <= now.AddMinutes(1)) return true;

                    return false;
                })
                // Sắp xếp ưu tiên:
                // 1. Thẻ chưa có Progress (Mới nhất) -> Rank 0
                // 2. Thẻ đang học/quên (Interval ~ 0) -> Rank 1
                // 3. Thẻ Review (Interval > 0) -> Rank 2
                .OrderBy(c => c.Progress == null ? 0 : (c.Progress.Interval < 0.01 ? 1 : 2))

                // Nếu cùng nhóm ưu tiên, lấy thẻ có DueDate cũ nhất trước
                .ThenBy(c => c.Progress == null ? DateTime.MinValue : c.Progress.DueDate)

                .FirstOrDefault();

            if (dueCard != null)
            {
                Debug.WriteLine($"[StudyService] Found card: {dueCard.FrontText} (ID: {dueCard.ID})");
            }
            else
            {
                Debug.WriteLine("[StudyService] No due card found.");
            }

            return dueCard;
        }

        // --- 2. LOGIC TÍNH TOÁN THỐNG KÊ (GIỮ NGUYÊN) ---
        public async Task<DeckStats> GetDeckStatsAsync(int deckId)
        {
            var deck = await _deckRepository.GetByIdAsync(deckId);
            var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
            var now = DateTime.Now;

            var newCount = cards.Count(c => c.Progress == null || c.Progress.Interval < 0.01);

            var learningCount = cards.Count(c => c.Progress != null &&
                                                 c.Progress.DueDate <= now &&
                                                 c.Progress.Interval >= 0.01 && c.Progress.Interval < 1);

            var reviewCount = cards.Count(c => c.Progress != null &&
                                               c.Progress.DueDate <= now &&
                                               c.Progress.Interval >= 1);

            return new DeckStats
            {
                DeckName = deck?.Name ?? "Unknown Deck",
                NewCount = newCount,
                LearningCount = learningCount,
                ReviewCount = reviewCount
            };
        }

        // --- 3. THUẬT TOÁN SM-2 (GIỮ NGUYÊN) ---
        public async Task ProcessReviewAsync(Card card, ReviewOutcome outcome)
        {
            if (card == null) return;

            if (card.Progress == null) card.Progress = new CardProgress();
            var p = card.Progress;

            switch (outcome)
            {
                case ReviewOutcome.Again:
                    p.Interval = 0;
                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.2);
                    break;

                case ReviewOutcome.Hard:
                    if (p.Interval == 0) p.Interval = 1;
                    else p.Interval = p.Interval * 1.2;
                    p.EaseFactor = Math.Max(1.3, p.EaseFactor - 0.15);
                    break;

                case ReviewOutcome.Good:
                    if (p.Interval == 0) p.Interval = 1;
                    else if (p.Interval == 1) p.Interval = 6;
                    else p.Interval = p.Interval * p.EaseFactor;
                    break;

                case ReviewOutcome.Easy:
                    if (p.Interval == 0) p.Interval = 4;
                    else if (p.Interval == 1) p.Interval = 15;
                    else p.Interval = p.Interval * p.EaseFactor * 1.3;
                    p.EaseFactor += 0.15;
                    break;
            }

            // Làm tròn tối thiểu
            if (p.Interval < 1 && p.Interval > 0) p.Interval = 1;

            // Cập nhật DueDate
            if (p.Interval == 0)
            {
                p.DueDate = DateTime.Now; // Học lại ngay
            }
            else
            {
                p.DueDate = DateTime.Now.AddDays(p.Interval);
            }

            await _cardRepository.UpdateAsync(card);
        }
    }
}