using CommunityToolkit.Mvvm.ComponentModel;
using Firebase.Auth;
using Firebase.Auth.Requests;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace EasyFlips.Services
{
    // Class này lưu trữ thông tin người dùng hiện tại (phiên làm việc)
    public partial class UserSession : ObservableObject
    {
        // Khi UserId thay đổi, atttribute này sẽ tự báo cho UI biết IsLoggedIn cũng đã đổi
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
        private string? userId;

        // Thuộc tính chứa ảnh để hiển thị lên UI
        [ObservableProperty]
        private BitmapImage _avatarBitmap;

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
            LoadAvatarImage(newAvatarUrl);
        }
        // [FIX] Hàm tải ảnh tối ưu(Fix lỗi không hiện + Fix lag)
        public async void LoadAvatarImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                // Dùng HttpClient tải dữ liệu về bộ nhớ đệm trước
                // Điều này đảm bảo ảnh tải xong mới hiện, không bị trắng
                using (var client = new HttpClient())
                {
                    var data = await client.GetByteArrayAsync(url);

                    // Chạy trên UI Thread để tạo Bitmap
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        using (var stream = new MemoryStream(data))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;

                            // Quan trọng: CacheOnLoad để lưu vào RAM, ngắt kết nối Stream
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;

                            // Quan trọng: Thu nhỏ ảnh xuống 350px để nhẹ RAM (Avatar chỉ cần nhỏ)
                            bitmap.DecodePixelWidth = 350;

                            bitmap.EndInit();
                            bitmap.Freeze(); // Đóng băng để dùng mượt mà

                            AvatarBitmap = bitmap; // Cập nhật lên giao diện
                        }
                    });
                }
            }
            catch
            {
                // Nếu lỗi (link chết, mất mạng), gán null để UI hiện ảnh mặc định (TargetNullValue)
                AvatarBitmap = null;
            }
        }

    }
}