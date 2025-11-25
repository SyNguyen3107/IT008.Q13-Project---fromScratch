using EasyFlips.Interfaces;
using EasyFlips.Models; // truy cập class Deck
using Microsoft.EntityFrameworkCore; // Cần cho ToListAsync, FindAsync, ..

namespace EasyFlips.Repositories
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
            var decks = await _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .ToListAsync();

            // 2. Tính toán thống kê cho từng Deck
            foreach (var deck in decks)
            {
                var now = DateTime.Now;

                // Đếm số thẻ New (CHƯA CÓ PROGRESS - chưa xem bao giờ)
                deck.NewCount = deck.Cards.Count(c => c.Progress == null);

                // Đếm số thẻ Learn (ĐÃ XEM nhưng chưa hoàn thành: 0 < Interval < 1)
                // Chỉ đếm card có Interval > 0 (đã học) và < 1 (chưa hoàn thành)
                deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.Interval > 0 &&
                                                    c.Progress.Interval < 1);

                // Đếm số thẻ Due/Review (Đã hoàn thành: Interval >= 1 VÀ đã đến hạn)
                deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                  c.Progress.Interval >= 1 &&
                                                  c.Progress.DueDate <= now);
            }
            return decks;
        }


        // Tìm Deck có ID cụ thể trong danh sách
        public async Task<Deck> GetByIdAsync(int id)
        {
            // Lấy Deck kèm theo Cards và Progress
            var deck = await _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .FirstOrDefaultAsync(d => d.ID == id);

            // Nếu tìm thấy deck thì tính toán thống kê
            if (deck != null)
            {
                var now = DateTime.Now;

                // Đếm số thẻ New (CHƯA CÓ PROGRESS - chưa xem bao giờ)
                deck.NewCount = deck.Cards.Count(c => c.Progress == null);

                // Đếm số thẻ Learn (ĐÃ XEM nhưng chưa hoàn thành: 0 < Interval < 1)
                deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.Interval > 0 &&
                                                    c.Progress.Interval < 1);

                // Đếm số thẻ Due/Review (Đã hoàn thành: Interval >= 1 VÀ đã đến hạn)
                deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                  c.Progress.Interval >= 1 &&
                                                  c.Progress.DueDate <= now);
            }

            return deck;
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
        public async Task<Deck?> GetByNameAsync(string name)
        {
            // Tìm deck đầu tiên có tên trùng khớp (không phân biệt hoa thường nếu muốn)
            return await _context.Decks.FirstOrDefaultAsync(d => d.Name == name);
        }
    }
}