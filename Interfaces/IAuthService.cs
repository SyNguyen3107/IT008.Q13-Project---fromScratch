using System.Threading.Tasks;

namespace EasyFlips.Interfaces
{
    public interface IAuthService
    {
        // Lấy ID của user hiện tại (trả về null nếu chưa đăng nhập)
        string CurrentUserId { get; }

        // Kiểm tra xem đã đăng nhập chưa
        bool IsLoggedIn { get; }

        // Đăng nhập: Trả về UserId nếu thành công
        Task<string> LoginAsync(string email, string password);

        // Đăng ký: Trả về UserId nếu thành công
        Task<string> RegisterAsync(string email, string password);

        // Đăng xuất
        void Logout();
    }
}