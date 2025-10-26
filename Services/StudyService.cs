// Thêm 2 'using' này ở đầu file StudyService.cs
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.EntityFrameworkCore; // Cần dùng cho .FirstOrDefaultAsync()
using System;
using System.Linq;
using System.Threading.Tasks;

public class StudyService
{
    private readonly ICardRepository _cardRepository;

    // Constructor của bạn (để DI tiêm vào)
    public StudyService(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    // === THÊM PHƯƠNG THỨC NÀY VÀO ===
    public async Task<Card> GetNextCardToReviewAsync(int deckId)
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

    // (Trong tương lai bạn sẽ thêm hàm ProcessReviewAsync ở đây)
}