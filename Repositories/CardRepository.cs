using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            // --- THÊM LOGIC QUAN TRỌNG ---
            // Đảm bảo rằng mọi thẻ MỚI khi được thêm vào
            // đều có một CardProgress mặc định.
            if (card.Progress == null)
            {
                card.Progress = new CardProgress();
                // (Constructor của CardProgress sẽ tự set giá trị mặc định)
            }
            // -----------------------------

            await _context.Cards.AddAsync(card);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            // FindAsync rất nhanh, nhưng không thể .Include
            // Nên ta sẽ tìm bằng FindAsync và Remove
            var card = await _context.Cards.FindAsync(id);
            if (card != null)
            {
                _context.Cards.Remove(card);
                await _context.SaveChangesAsync();
                // (Database đã được setup OnDelete(Cascade)
                // nên CardProgress cũng sẽ tự động bị xóa theo)
            }
        }

        public async Task<Card> GetByIdAsync(int id)
        {
            // --- ĐÃ SỬA LỖI ---
            // Dùng FirstOrDefaultAsync VÀ .Include để lấy được Progress
            // FindAsync không thể .Include
            return await _context.Cards
                .Include(c => c.Progress) // Tải kèm Progress
                .FirstOrDefaultAsync(c => c.ID == id);
        }

        public async Task<List<Card>> GetCardsByDeckIdAsync(int deckId)
        {
            // --- ĐÃ SỬA LỖI ---
            // Dùng LINQ để lọc ra các thẻ có DeckId mong muốn
            return await _context.Cards
                .Where(c => c.DeckId == deckId)
                .Include(c => c.Progress) // <-- Tải kèm Progress
                .ToListAsync();
        }

        public async Task UpdateAsync(Card card)
        {
            // Đánh dấu thẻ này là đã bị thay đổi
            // EF Core sẽ tự động phát hiện thay đổi trên card.Progress
            _context.Entry(card).State = EntityState.Modified;

            // Nếu bạn muốn chắc chắn hơn:
            if (card.Progress != null)
            {
                _context.Entry(card.Progress).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
        }
    }
}