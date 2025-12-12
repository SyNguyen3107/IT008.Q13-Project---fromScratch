using EasyFlips.Models;

namespace EasyFlips.Interfaces
{
    public interface IDeckRepository
    {
        // Thêm 1 deck mới vào database
        Task AddAsync(Deck deck);
        // Lấy danh sách tất cả deck
        Task<IEnumerable<Deck>> GetAllAsync();
        // Lấy 1 deck theo ID
        Task<Deck> GetByIdAsync(string id);
        // Cập nhật thông tin deck
        Task UpdateAsync(Deck deck);
        // Xóa deck theo ID
        Task DeleteAsync(string id);
        //Kiểm tra tên deck đã tồn tại chưa
        Task<Deck?> GetByNameAsync(string name);
    }

}
