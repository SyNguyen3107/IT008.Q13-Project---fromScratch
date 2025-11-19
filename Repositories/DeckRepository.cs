using System.Collections.Generic; // dùng cho List<T> và IEnumrable<T>
using System.Threading.Tasks; // cho phép viết hàm bất đồng bộ (async)
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models; // truy cập class Deck
using Microsoft.EntityFrameworkCore; // Cần cho ToListAsync, FindAsync, ...
using System.Linq; // Cần cho .Where, v.v.
using System;//Cần cho DateTime

namespace IT008.Q13_Project___fromScratch.Repositories
{
    public class DeckRepository : IDeckRepository
    {
        private readonly AppDbContext _context;

        public DeckRepository(AppDbContext context)
        {
            _context = context; // Constructor này là chính xác
        }

        public async Task AddAsync(Deck deck)
        {
            // Code đúng: Thêm vào DbContext và Lưu
            await _context.Decks.AddAsync(deck);
            await _context.SaveChangesAsync();
        }

        // Lấy toàn bộ danh sách Deck hiện có
        public async Task<IEnumerable<Deck>> GetAllAsync()
        {
            // 1. Lấy tất cả Deck kèm theo Cards và Progress của chúng
            // Lưu ý: Nếu dữ liệu quá lớn, cách này có thể chậm, nhưng với đồ án thì OK.
            var decks = await _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .ToListAsync();
            // 2. Tính toán thống kê cho từng Deck
            foreach (var deck in decks)
            {
                var now = DateTime.Now;

                // Đếm số thẻ New (Chưa có progress hoặc Interval = 0)
                deck.NewCount = deck.Cards.Count(c => c.Progress == null || c.Progress.Interval == 0);

                // Đếm số thẻ Learn (Đang học dở: đến hạn VÀ interval nhỏ < 1 ngày)
                deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.DueDate <= now &&
                                                    c.Progress.Interval < 1 &&
                                                    c.Progress.Interval > 0);

                // Đếm số thẻ Due/Review (Đến hạn ôn tập: đến hạn VÀ interval >= 1 ngày)
                deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                  c.Progress.DueDate <= now &&
                                                  c.Progress.Interval >= 1);
            }
            return decks;
        }


        // Tìm Deck có ID cụ thể trong danh sách
        public async Task<Deck> GetByIdAsync(int id)
        {
            // Code đúng: Dùng FindAsync là cách nhanh nhất
            return await _context.Decks.FindAsync(id);
        }

        // Cập nhật Deck hiện có
        public async Task UpdateAsync(Deck deck)
        {
            // Code đúng: Báo cho EF Core biết đối tượng này đã thay đổi
            _context.Entry(deck).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        // Xóa Deck có ID tương ứng
        public async Task DeleteAsync(int id)
        {
            // Code đúng: Tìm Deck và Xóa
            var deck = await _context.Decks.FindAsync(id);
            if (deck != null)
            {
                _context.Decks.Remove(deck);
                await _context.SaveChangesAsync();
                // (OnDelete(Cascade) đã được setup trong DbContext
                // nên khi xóa Deck, CSDL sẽ tự xóa các Card liên quan)
            }
        }
    }
}