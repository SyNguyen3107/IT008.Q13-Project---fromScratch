// Thêm 2 'using' này ở đầu file StudyService.cs
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.EntityFrameworkCore; // Cần dùng cho .FirstOrDefaultAsync()
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Markup;

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

    // === THÊM PHƯƠNG THỨC NÀY VÀO ===
    public async Task<Card?> GetNextCardToReviewAsync(int deckId)
    {
        // 1. Lấy tất cả thẻ của Deck
        var allCards = await _cardRepository.GetCardsByDeckIdAsync(deckId);

        // 2. Tìm thẻ cần học (có DueDate đã qua)
        var dueCard = allCards
            .Where(c => c.DueDate <= DateTime.Today) // Lọc các thẻ đã "đến hạn"
            .OrderBy(c => c.DueDate) // Ưu tiên thẻ cũ nhất
            .FirstOrDefault(); // Lấy thẻ đầu tiên

        return dueCard; // Trả về thẻ (hoặc null nếu không còn thẻ nào)
    }

    // Xử lý kết quả ôn tập cho một thẻ: cập nhật Interval, EaseFactor và DueDate rồi lưu lại
    public async Task<Card> ProcessReviewAsync(Card card, ReviewOutcome outcome)
    {
        if (card == null) throw new ArgumentException(nameof(card));

        // Thiết lập mặc định nếu các trường chưa có giá trị hợp lý
        if (card.EaseFactor <= 0) card.EaseFactor = 2.5;
        if (card.Interval <= 0) card.Interval = 1;

        // Điều chỉnh Interval và EaseFactor theo outcome (công thức đơn giản, có thể tinh chỉnh sau)
        switch (outcome)
        {
            case ReviewOutcome.Again:
                // Lỗi, học lại gần ngay (đặt lại interval nhỏ)
                card.Interval = 1;
                // Khi "Again" thường giảm EF nhẹ 
                card.EaseFactor = Math.Max(1.3, card.EaseFactor - 0.2);
                break;
            case ReviewOutcome.Hard:
                // Khó: tăng nhẹ interval so với hiện tại
                card.Interval = Math.Max(1, Math.Round(card.Interval * 1.2));
                card.EaseFactor = Math.Max(1.3, card.EaseFactor - 0.15);
                break;
            case ReviewOutcome.Good:
                // Tốt: nhân interval
                card.Interval = Math.Max(1, Math.Round(card.Interval * 2.5));
                card.EaseFactor = Math.Max(1.3, card.EaseFactor + 0.05);
                break;
            case ReviewOutcome.Easy:
                // Dễ: tăng hơn nữa
                card.Interval = Math.Max(1, Math.Round(card.Interval * 3.5));
                card.EaseFactor = Math.Max(1.3, card.EaseFactor + 0.15);
                break;

            default:
                break;
        }

        // Cập nhật ngày đến hạn tiếp theo
        card.DueDate = DateTime.Today.AddDays(card.Interval);

        // Lưu thay đổi vào repository (CSDl)
        await _cardRepository.UpdateAsync(card);

        return card;
    }
}