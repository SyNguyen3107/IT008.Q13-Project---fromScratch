using EasyFlips.Models;

namespace EasyFlips.Interfaces
{
    public interface ICardRepository
    {
        // Lấy một thẻ theo ID
        Task<Card> GetByIdAsync(string id);

        // Lấy tất cả các thẻ của một Deck cụ thể
        Task<List<Card>> GetCardsByDeckIdAsync(string deckId);

        // Thêm một thẻ mới
        Task AddAsync(Card card);

        // Cập nhật một thẻ đã có
        Task UpdateAsync(Card card);

        // Xóa một thẻ
        Task DeleteAsync(string id);
    }
}