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

    // Constructor của bạn (để DI tiêm vào)
    public StudyService(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    // === PHƯƠNG THỨC ĐÃ SỬA LỖI ===
    public async Task<Card?> GetNextCardToReviewAsync(int deckId)
    {
        // 1. Lấy tất cả thẻ của Deck
        // QUAN TRỌNG: GetCardsByDeckIdAsync BẮT BUỘC phải .Include(c => c.Progress)
        // (Chúng ta sẽ sửa điều này ở file CardRepository.cs)
        var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);

        // 2. Tìm thẻ cần học (có DueDate đã qua)
        var dueCard = allCards
            // THAY ĐỔI: Truy cập qua c.Progress
            .Where(c => c.Progress != null && c.Progress.DueDate <= DateTime.Today)
            // THAY ĐỔI: Truy cập qua c.Progress
            .OrderBy(c => c.Progress.DueDate)
            .FirstOrDefault(); // Lấy thẻ đầu tiên

        return dueCard; // Trả về thẻ (hoặc null nếu không còn thẻ nào)
    }

    // === PHƯƠNG THỨC ĐÃ SỬA LỖI ===
    public async Task<Card> ProcessReviewAsync(Card card, ReviewOutcome outcome)
    {
        if (card == null) throw new ArgumentException(nameof(card));

        // Rất quan trọng: phải có đối tượng Progress
        if (card.Progress == null)
        {
            // Nếu vì lý do gì đó mà thẻ chưa có progress, tạo mới
            card.Progress = new CardProgress();
        }

        // THAY ĐỔI: Mọi truy cập đều qua card.Progress
        var progress = card.Progress;

        // Thiết lập mặc định nếu các trường chưa có giá trị hợp lý
        if (progress.EaseFactor <= 0) progress.EaseFactor = 2.5;
        if (progress.Interval <= 0) progress.Interval = 1; // Bắt đầu với 1 ngày

        // Điều chỉnh Interval và EaseFactor theo outcome
        switch (outcome)
        {
            case ReviewOutcome.Again:
                // Lỗi, học lại gần ngay
                progress.Interval = 0; // Đặt về 0 để học lại ngay trong hôm nay (hoặc 1 nếu muốn học vào ngày mai)
                progress.EaseFactor = Math.Max(1.3, progress.EaseFactor - 0.2);
                break;
            case ReviewOutcome.Hard:
                // Khó: tăng nhẹ interval
                progress.Interval = Math.Max(1, Math.Round(progress.Interval * 1.2));
                progress.EaseFactor = Math.Max(1.3, progress.EaseFactor - 0.15);
                break;
            case ReviewOutcome.Good:
                // Tốt: nhân interval theo EaseFactor
                progress.Interval = Math.Max(1, Math.Round(progress.Interval * progress.EaseFactor));
                break;
            case ReviewOutcome.Easy:
                // Dễ: tăng hơn nữa
                progress.Interval = Math.Max(1, Math.Round(progress.Interval * progress.EaseFactor * 1.3));
                progress.EaseFactor = Math.Max(1.3, progress.EaseFactor + 0.15);
                break;

            default:
                break;
        }

        // Cập nhật ngày đến hạn tiếp theo
        progress.DueDate = DateTime.Today.AddDays(progress.Interval);

        // Lưu thay đổi vào repository (CSDl)
        // Bạn chỉ cần Update(card). EF Core đủ thông minh để biết
        // đối tượng "Progress" liên quan đã bị thay đổi và tự lưu lại.
        await _cardRepository.UpdateAsync(card);

        return card;
    }
}