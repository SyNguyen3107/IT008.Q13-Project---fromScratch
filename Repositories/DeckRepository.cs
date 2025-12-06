using EasyFlips.Interfaces;
using EasyFlips.Models; // Truy cập class Deck
using Microsoft.EntityFrameworkCore; // Cần cho ToListAsync, FindAsync, ...

namespace EasyFlips.Repositories
{
    /// <summary>
    /// Repository quản lý các thao tác CRUD với Deck trong cơ sở dữ liệu
    /// </summary>
    public class DeckRepository : IDeckRepository
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;

        /// <summary>
        /// Khởi tạo DeckRepository với database context và auth service
        /// </summary>
        public DeckRepository(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// Thêm một Deck mới vào cơ sở dữ liệu
        /// </summary>
        /// <param name="deck">Deck cần thêm</param>
        public async Task AddAsync(Deck deck)
        {
            // Gán UserId hiện tại cho deck mới nếu người dùng đã đăng nhập
            if (_authService?.IsLoggedIn == true)
            {
                deck.UserId = _authService.CurrentUserId;
            }
            
            // Thêm deck vào DbContext và lưu thay đổi vào database
            await _context.Decks.AddAsync(deck);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Gán quyền sở hữu tất cả deck ẩn danh cho user hiện tại
        /// Sử dụng khi user đăng nhập và muốn nhận quyền sở hữu các deck đã tạo trước đó
        /// </summary>
        public async Task ClaimData()
        {
            if(_authService?.IsLoggedIn == true)
            {
                var uid = _authService.CurrentUserId;
                
                // Tìm tất cả Deck không có UserId (dữ liệu ẩn danh)
                var anonymousDecks = await _context.Decks
                    .Where(d => d.UserId == null)
                    .ToListAsync();
                
                // Gán UserId hiện tại cho tất cả Deck ẩn danh
                foreach (var deck in anonymousDecks)
                {
                    deck.UserId = uid;
                }
                
                // Lưu thay đổi vào database
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả Deck (của user hiện tại nếu đã đăng nhập)
        /// Kèm theo thống kê số lượng thẻ New, Learn, Due
        /// </summary>
        /// <returns>Danh sách Deck với thống kê đầy đủ</returns>
        public async Task<IEnumerable<Deck>> GetAllAsync()
        {
            // 1. Tạo query lấy Deck kèm theo Cards và Progress
            var query = _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .AsQueryable();

            // Nếu đã đăng nhập, chỉ lấy deck của user hiện tại
            if (_authService?.IsLoggedIn == true && !string.IsNullOrEmpty(_authService.CurrentUserId))
            {
                var uid = _authService.CurrentUserId;
                query = query.Where(d => d.UserId == uid);
            }

            var decks = await query.ToListAsync();

            // 2. Tính toán thống kê cho từng Deck
            foreach (var deck in decks)
            {
                var now = DateTime.Now;

                // Đếm số thẻ New (CHƯA CÓ PROGRESS - chưa học bao giờ)
                deck.NewCount = deck.Cards.Count(c => c.Progress == null);

                // Đếm số thẻ Learn (ĐÃ HỌC nhưng chưa thành thạo: 0 < Interval < 1)
                // Interval > 0 nghĩa là đã xem ít nhất 1 lần
                // Interval < 1 nghĩa là chưa đến mức ôn tập theo ngày
                deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.Interval > 0 &&
                                                    c.Progress.Interval < 1);

                // Đếm số thẻ Due/Review (ĐÃ THÀNH THẠO nhưng đến hạn ôn tập)
                // Interval >= 1 nghĩa là đã học thành thạo (chu kỳ ôn tập >= 1 ngày)
                // DueDate <= now nghĩa là đã đến hạn cần ôn tập lại
                deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                  c.Progress.Interval >= 1 &&
                                                  c.Progress.DueDate <= now);
            }
            
            return decks;
        }

        /// <summary>
        /// Lấy một Deck theo ID kèm theo thống kê
        /// </summary>
        /// <param name="id">ID của Deck cần lấy</param>
        /// <returns>Deck với thống kê đầy đủ hoặc null nếu không tìm thấy</returns>
        public async Task<Deck> GetByIdAsync(int id)
        {
            // Lấy Deck kèm theo Cards và Progress của từng card
            var deck = await _context.Decks
                .Include(d => d.Cards)
                .ThenInclude(c => c.Progress)
                .FirstOrDefaultAsync(d => d.ID == id);

            // Nếu tìm thấy deck thì tính toán thống kê
            if (deck != null)
            {
                var now = DateTime.Now;

                // Đếm số thẻ New (CHƯA CÓ PROGRESS - chưa học bao giờ)
                deck.NewCount = deck.Cards.Count(c => c.Progress == null);

                // Đếm số thẻ Learn (ĐÃ HỌC nhưng chưa thành thạo: 0 < Interval < 1)
                deck.LearnCount = deck.Cards.Count(c => c.Progress != null &&
                                                    c.Progress.Interval > 0 &&
                                                    c.Progress.Interval < 1);

                // Đếm số thẻ Due/Review (ĐÃ THÀNH THẠO nhưng đến hạn ôn tập)
                deck.DueCount = deck.Cards.Count(c => c.Progress != null &&
                                                  c.Progress.Interval >= 1 &&
                                                  c.Progress.DueDate <= now);
            }

            return deck;
        }

        /// <summary>
        /// Cập nhật thông tin của một Deck hiện có
        /// </summary>
        /// <param name="deck">Deck với thông tin mới cần cập nhật</param>
        public async Task UpdateAsync(Deck deck)
        {
            // Đánh dấu đối tượng deck đã bị thay đổi để EF Core biết cần cập nhật
            _context.Entry(deck).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa một Deck theo ID
        /// Các Card liên quan sẽ tự động bị xóa do cấu hình Cascade Delete
        /// </summary>
        /// <param name="id">ID của Deck cần xóa</param>
        public async Task DeleteAsync(int id)
        {
            // Tìm Deck theo ID
            var deck = await _context.Decks.FindAsync(id);
            if (deck != null)
            {
                // Xóa deck khỏi database
                _context.Decks.Remove(deck);
                await _context.SaveChangesAsync();
                
                // Lưu ý: OnDelete(Cascade) đã được cấu hình trong DbContext
                // nên khi xóa Deck, database sẽ tự động xóa các Card và CardProgress liên quan
            }
        }

        /// <summary>
        /// Tìm Deck theo tên chính xác
        /// </summary>
        /// <param name="name">Tên của Deck cần tìm</param>
        /// <returns>Deck đầu tiên có tên trùng khớp hoặc null nếu không tìm thấy</returns>
        public async Task<Deck?> GetByNameAsync(string name)
        {
            // Tìm deck đầu tiên có tên trùng khớp (phân biệt hoa thường)
            return await _context.Decks.FirstOrDefaultAsync(d => d.Name == name);
        }
    }
}