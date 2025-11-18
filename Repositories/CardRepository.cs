using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IT008.Q13_Project___fromScratch.Models;

namespace IT008.Q13_Project___fromScratch.Repositories
{
    public class CardRepository : ICardRepository
    {
        private readonly AppDbContext _context;

        // Nhận AppDbContext thông qua Dependency Injection
        public CardRepository(AppDbContext context)
        {
            _context = context;
        }

        // --- Triển khai các phương thức ---

        public async Task AddAsync(Card card)
        {
            await _context.Cards.AddAsync(card);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var card = await _context.Cards.FindAsync(id);
            if (card != null)
            {
                _context.Cards.Remove(card);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Card> GetByIdAsync(int id)
        {
            // FindAsync là cách nhanh nhất để lấy bằng Khóa chính
            return await _context.Cards.FindAsync(id);
        }

        public async Task<List<Card>> GetCardsByDeckIdAsync(int deckId)
        {
            // Dùng LINQ để lọc ra các thẻ có DeckId mong muốn
            return await _context.Cards
                .Where(c => c.DeckId == deckId)
                .ToListAsync();
        }

        public async Task UpdateAsync(Card card)
        {
            // Đánh dấu thẻ này là đã bị thay đổi
            _context.Entry(card).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}