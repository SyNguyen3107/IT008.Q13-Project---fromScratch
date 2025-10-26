using System.Collections.Generic;
using System.Threading.Tasks;
using IT008.Q13_Project___fromScratch.Models;

namespace IT008.Q13_Project___fromScratch.Repositories
{
    public interface ICardRepository
    {
        // Thêm thẻ mới
        Task AddAsync(Card card);
        // Cập nhật front/back
        Task UpdateAsync(Card card);
        // Xóa thẻ
        Task DeleteAsync(int id);
        // Lấy 1 thẻ cụ thể
        Task<Card> GetByIdAsync(int id);
        // Lấy tất cả thẻ trong 1 deck
        Task<IEnumerable<Card>> GetByDeckIdAsync(int deckID);
    }
}
