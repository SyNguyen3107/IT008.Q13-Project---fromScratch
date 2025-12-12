using CommunityToolkit.Mvvm.ComponentModel;
using Firebase.Auth;
using Firebase.Auth.Requests;
using Newtonsoft.Json.Linq;

namespace EasyFlips.Services
{
    // Class này lưu trữ thông tin người dùng hiện tại (phiên làm việc)
    public partial class UserSession : ObservableObject
    {
        // Khi UserId thay đổi, atttribute này sẽ tự báo cho UI biết IsLoggedIn cũng đã đổi
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
        private string? userId;

        [ObservableProperty]
        private string? email;

        [ObservableProperty]
        private string? token;

        [ObservableProperty]
        private string? userName;

        [ObservableProperty]
        private string? avatarURL;
        public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);
        [ObservableProperty]
        private string? refreshToken;
        // Hàm set thông tin khi đăng nhập thành công
        public void SetUser(string userId, string email, string token, string refreshToken, string name = "", string photo = "")
        {
            UserId = userId;
            Email = email;
            Token = token;
            RefreshToken = refreshToken;
            UserName = name;
            AvatarURL = photo;
        }

        // Hàm xóa thông tin khi đăng xuất
        public void Clear()
        {
            // Gán vào Property để kích hoạt thông báo cập nhật UI
            UserId = null;
            Email = null;
            Token = null;
            RefreshToken = null;
            UserName = null;
            AvatarURL = null;
        }
        // Hàm này dùng để cập nhật giao diện ngay sau khi sửa profile thành công
        // mà không cần đăng nhập lại
        public void UpdateUserInfo(string newName, string newAvatarUrl)
        {
            if (!string.IsNullOrEmpty(newName))
            {
                UserName = newName;
            }

            if (!string.IsNullOrEmpty(newAvatarUrl))
            {
                AvatarURL = newAvatarUrl;
            }
        }
    }
}