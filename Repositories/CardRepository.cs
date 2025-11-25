using EasyFlips.Interfaces;
using EasyFlips.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyFlips.Repositories
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
            // KHÔNG tự động tạo Progress cho card mới
            // Card mới phải có Progress = null để được tính vào NewCount
            // Progress chỉ được tạo khi người dùng học card lần đầu tiên (trong StudyService.ProcessReviewAsync)

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
            // Đánh dấu thẻ đã thay đổi
            _context.Entry(card).State = EntityState.Modified;

            if (card.Progress != null)
            {
                // Nếu Progress mới tạo (chưa có ID) -> thêm mới
                if (card.Progress.ID == 0)
                {
                    // đảm bảo gắn khóa ngoại
                    card.Progress.CardId = card.ID;
                    _context.Entry(card.Progress).State = EntityState.Added; // thêm mới
                }
                else
                {
                    _context.Entry(card.Progress).State = EntityState.Modified; // cập nhật
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}