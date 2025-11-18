using IT008.Q13_Project___fromScratch.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.Interfaces
{
    public interface ICardRepository
    {
        // Lấy một thẻ theo ID
        Task<Card> GetByIdAsync(int id);

        // Lấy tất cả các thẻ của một Deck cụ thể
        Task<List<Card>> GetCardsByDeckIdAsync(int deckId);

        // Thêm một thẻ mới
        Task AddAsync(Card card);

        // Cập nhật một thẻ đã có
        Task UpdateAsync(Card card);

        // Xóa một thẻ
        Task DeleteAsync(int id);
    }
}