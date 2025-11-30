namespace EasyFlips.Services
{
    // Class này lưu trữ thông tin người dùng hiện tại (phiên làm việc)
    public class UserSession
    {
        public string? UserId { get; private set; }
        public string? Email { get; private set; }
        public string? Token { get; private set; } // Firebase Token

        public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);

        // Hàm set thông tin khi đăng nhập thành công
        public void SetUser(string userId, string email, string token)
        {
            UserId = userId;
            Email = email;
            Token = token;
        }

        // Hàm xóa thông tin khi đăng xuất
        public void Clear()
        {
            UserId = null;
            Email = null;
            Token = null;
        }
    }
}