using System.Threading.Tasks;

namespace EasyFlips.Interfaces
{
    public interface IAuthService
    {
        // Lấy ID của user hiện tại
        string CurrentUserId { get; }

        // Kiểm tra xem đã đăng nhập chưa
        bool IsLoggedIn { get; }

        // Đăng nhập: Trả về true nếu thành công
        // [FIX]: Chuyển từ Task<string> sang Task<bool>
        Task<bool> LoginAsync(string email, string password);

        // Đăng ký: Trả về true nếu thành công
        // [FIX]: Chuyển từ Task<string> sang Task<bool>
        Task<bool> RegisterAsync(string email, string password, string username);

        // Đăng xuất (Async)
        // [FIX]: Chuyển từ void Logout() sang Task LogoutAsync()
        Task LogoutAsync();

        // [FIX]: Thêm hàm khôi phục phiên đăng nhập
        bool RestoreSession();
    }
}