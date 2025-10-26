using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IT008.Q13_Project___fromScratch.Models;

namespace IT008.Q13_Project___fromScratch.Repositories
{
    public class CardRepository : ICardRepository
    {
        private readonly List<Card> _cards = new();
        public async Task AddAsync(Card card)
        {
            await Task.Delay(50); // Bộ nhớ tạm trong RAM 
            _cards.Add(card); // Thêm thẻ vào DS card
        }
        public async Task UpdateAsync(Card card)
        {
            await Task.Delay(50);
            var existing = _cards.FirstOrDefault(c => c.ID == card.ID); // Tìm thẻ có ID trùng với thẻ gửi lên
            if (existing != null) // nếu thấy, cập nhật Front & Back
            {
                existing.FrontText = card.FrontText;
                existing.BackText  = card.BackText;
            }
        }
        public async Task DeleteAsync(int id)
        {
            await Task.Delay(50);
            _cards.RemoveAll(c => c.ID == id); // Tìm thẻ có ID trùng với tham số id để xóa chúng
        }

        public async Task<Card> GetByIdAsync(int id) // Tìm thẻ theo ID 
        {
            await Task.Delay(50);
            return _cards.FirstOrDefault(c => c.ID == id);
        }

        public async Task<IEnumerable<Card>> GetByDeckIdAsync(int deckID)
        {
            await Task.Delay(50);
            return _cards.Where(c => c.DeckId == deckID).ToList(); // Lọc tất cả các thẻ có DeckId trùng với deckID
        }
    }
}
