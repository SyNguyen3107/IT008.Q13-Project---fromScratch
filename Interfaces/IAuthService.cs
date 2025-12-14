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

        // [NEW]: Tải thông tin profile (display_name, avatar_url) từ bảng profiles
        Task LoadProfileInfoAsync();

        // [NEW]: Gửi email reset mật khẩu
        Task<bool> ForgotPasswordAsync(string email);

        // [NEW]: Gửi magic link đăng nhập
        Task<bool> SendMagicLinkAsync(string email);

        // [NEW]: Đổi mật khẩu sau khi đăng nhập bằng magic link
        Task<bool> UpdatePasswordAsync(string newPassword);

        bool IsRecoverySession();
        // [NEW]: Gửi mã OTP qua email
        Task<bool> SendOtpAsync(string email);

        // [NEW]: Xác thực mã OTP
        Task<bool> VerifyOtpAsync(string email, string token);

    }
}