using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.EntityFrameworkCore; // Cần dùng cho .FirstOrDefaultAsync()
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Windows.Markup;

// Enum này rất tốt, giữ nguyên
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

    // Constructor của bạn (để DI tiêm vào)
    public StudyService(ICardRepository cardRepository, IDeckRepository deckRepository)
    {
        _cardRepository = cardRepository;
        _deckRepository = deckRepository;
    }
    // --- 1. LOGIC LẤY THẺ CẦN HỌC ---
    public async Task<Card?> GetNextCardToReviewAsync(int deckId)
    {
        // 1. Lấy tất cả thẻ của Deck
        // QUAN TRỌNG: GetCardsByDeckIdAsync BẮT BUỘC phải .Include(c => c.Progress)
        // (Chúng ta sẽ sửa điều này ở file CardRepository.cs)
        var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);

        // 2. Tìm thẻ cần học (có DueDate đã qua)
        var dueCard = allCards
            .Where(c => c.Progress == null || c.Progress.DueDate <= DateTime.Now) //chưa học hoặc đã đến hạn
                .OrderBy(c => c.Progress == null ? DateTime.MinValue : c.Progress.DueDate) // Ưu tiên thẻ chưa học hoặc thẻ cũ nhất
                .FirstOrDefault(); // Lấy thẻ đầu tiên (nếu có)

        return dueCard; // Trả về thẻ (hoặc null nếu không còn thẻ nào)
    }
    public async Task<DeckStats> GetDeckStatsAsync(int deckId)
    {
        var deck = await _deckRepository.GetByIdAsync(deckId);
        var cards = await _cardRepository.GetCardsByDeckIdAsync(deckId);
        var now = DateTime.Now;

        var stats = new DeckStats
        {
            DeckName = deck?.Name ?? "Unknown Deck",
            // New: Thẻ chưa có Progress hoặc Interval = 0 (chưa học hoặc vừa quên)
            NewCount = cards.Count(c => c.Progress == null || c.Progress.Interval == 0),

            // Learning: Thẻ đã đến hạn VÀ đang trong giai đoạn học ngắn hạn (0 < Interval < 1 ngày)
            LearningCount = cards.Count(c => c.Progress != null && c.Progress.DueDate <= now && c.Progress.Interval < 1 && c.Progress.Interval > 0),

            // Review: Thẻ đã đến hạn VÀ là thẻ ôn tập dài hạn (Interval >= 1 ngày)
            ReviewCount = cards.Count(c => c.Progress != null && c.Progress.DueDate <= now && c.Progress.Interval >= 1)
        };
        return stats;
    }
    // --- 3. THUẬT TOÁN SM-2 (LOGIC CỐT LÕI) ---
    public async Task ProcessReviewAsync(Card card, ReviewOutcome outcome)
    {
        if (card == null) return;

        if (card.Progress == null) card.Progress = new CardProgress();
        var progress = card.Progress;

        // Rất quan trọng: phải có đối tượng Progress

        // Điều chỉnh Interval và EaseFactor theo outcome
        switch (outcome)
        {
            case ReviewOutcome.Again:
                // Quên: Reset về đầu
                progress.Interval = 0; // Đặt về 0 để học lại ngay trong hôm nay (hoặc 1 nếu muốn học vào ngày mai)
                progress.EaseFactor = Math.Max(1.3, progress.EaseFactor - 0.2);
                break;
            case ReviewOutcome.Hard:
                // Khó: tăng nhẹ interval
                // Nếu đang học (Interval=0), lên 1 ngày
                if (progress.Interval == 0) progress.Interval = 1;
                else progress.Interval = progress.Interval * 1.2;

                progress.EaseFactor = Math.Max(1.3, progress.EaseFactor - 0.15);
                break;
            case ReviewOutcome.Good:
                // Tốt: Tăng theo EaseFactor (SM-2 chuẩn)
                if (progress.Interval == 0) progress.Interval = 1;
                else if (progress.Interval == 1) progress.Interval = 6;
                else progress.Interval = progress.Interval * progress.EaseFactor;
                break;
            case ReviewOutcome.Easy:
                // Dễ: Tăng nhanh (Bonus)
                if (progress.Interval == 0) progress.Interval = 4;
                else if (progress.Interval == 1) progress.Interval = 15; // Nhảy cóc
                else progress.Interval = progress.Interval * progress.EaseFactor * 1.3;

                progress.EaseFactor += 0.15;
                break;
        }

        // Xử lý logic làm tròn và giới hạn Interval
        if (progress.Interval < 1 && progress.Interval > 0)
        {
            // Learning steps -> làm tròn lên 1 ngày để đơn giản hóa
            progress.Interval = 1;
        }

        // Cập nhật DueDate
        if (progress.Interval == 0)
        {
            // Nếu Again: DueDate là ngay bây giờ
            progress.DueDate = DateTime.Now;
        }
        else
        {
            progress.DueDate = DateTime.Now.AddDays(progress.Interval);
        }

        // Lưu thay đổi vào CSDL
        await _cardRepository.UpdateAsync(card);
    }
}