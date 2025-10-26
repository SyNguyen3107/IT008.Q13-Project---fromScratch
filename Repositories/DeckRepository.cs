using System.Collections.Generic; // dùng cho List<T> và IEnumrable<T>
using System.Threading.Tasks; // cho phép viết hàm bất đồng bộ (async)
using IT008.Q13_Project___fromScratch.Models; // truy cập class Deck

namespace IT008.Q13_Project___fromScratch.Repositories
{
    internal class DeckRepository : IDeckRepository
    {
        private readonly List<Deck> _decks = new();

        // DS tạm thời để lưu tất cả các Deck, dùng RAM để mô phỏng (readonly: _decks chỉ được khởi tạo 1 lần)
        public async Task AddAsync(Deck deck)
        {
            // Giả sử DB lưu trên bộ nhớ tạm
            await Task.Delay(50); // mô phỏng async I/O, nếu kết nối DB thật thay bằng await _context.Decks.AddAsync(deck);
            _decks.Add(deck);
        }
        // Lấy toàn bộ danh sách Deck hiện có
        public async Task<IEnumerable<Deck>> GetAllAsync() //IEnumberable<Deck> để có thể duyệt bằng foreach
        {
            await Task.Delay(50);
            return _decks;
        }
        // Tìm Deck có ID cụ thể trong danh sách
        public async Task<Deck> GetByIdAsync(int id)
        {
            await Task.Delay(50);
            return _decks.Find(d => d.ID == id);
        }
        // Cập nhật Deck hiện có (đã tồn tại trong DS)
        public async Task UpdateAsync(Deck deck)
        {
            await Task.Delay(50);
            var existing = _decks.Find(d => d.ID == deck.ID);
            if (existing != null)
            {
                existing.Name = deck.Name;
                existing.Description = deck.Description;
            }
        }
        // Xóa Deck có ID tương ứng khỏi _decks
        public async Task DeleteAsync(int id)
        {
            await Task.Delay(50);
            _decks.RemoveAll(d => d.ID == id);
        }
    }
}
