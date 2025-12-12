using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services;
using Microsoft.Win32;    // Dùng cho OpenFileDialog của WPF
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFlips.ViewModels
{
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;
        private readonly SupabaseService _supabaseService;

        // Hành động để báo cho View biết là cần đóng cửa sổ (dùng cho nút Cancel)
        public Action CloseAction { get; set; }

        [ObservableProperty]
        private string userName;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string avatarURL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))] // Để disable nút khi đang lưu
        private bool isBusy;
        public bool IsNotBusy => !IsBusy;

        // Biến này để lưu đường dẫn file ảnh trên máy tính (để upload sau này)
        private string _selectedLocalImagePath;

        // Constructor: Nhận vào UserSession để lấy dữ liệu hiện tại
        public EditProfileViewModel(UserSession userSession)
        {
            _userSession = userSession;
            _supabaseService = App.ServiceProvider.GetRequiredService<SupabaseService>();

            LoadUserData();
        }

        private void LoadUserData()
        {
            if (_userSession != null)
            {
                UserName = _userSession.UserName;
                Email = _userSession.Email;
                AvatarURL = _userSession.AvatarURL;
            }
            else
            {
                // Nếu đang test giao diện mà chưa có UserSession thật
                Email = "demo@easyflips.com";
            }
            _selectedLocalImagePath = null; // Reset ảnh chọn tạm
        }

        [RelayCommand]
        public void ChangeAvatar()
        {
            // Mở hộp thoại chọn file của Windows
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";

            if (openFileDialog.ShowDialog() == true)
            {
                // 1. Lưu đường dẫn file để tí nữa upload
                _selectedLocalImagePath = openFileDialog.FileName;

                // 2. Hiển thị ngay lên giao diện (Preview)
                AvatarURL = _selectedLocalImagePath;
            }
        }

        [RelayCommand]
        public async Task SaveProfile()
        {
            if (IsBusy) return; // Chặn bấm liên tục

            // [FIX] Kiểm tra UserId hợp lệ trước khi gọi API
            if (string.IsNullOrEmpty(_userSession?.UserId))
            {
                MessageBox.Show("Không tìm thấy thông tin người dùng. Vui lòng đăng nhập lại.", "Lỗi");
                return;
            }

            IsBusy = true;

            try
            {
                string finalAvatarUrl = AvatarURL;

                // [QUAN TRỌNG]: Nếu user đã chọn ảnh mới từ máy, upload lên Supabase
                if (!string.IsNullOrEmpty(_selectedLocalImagePath) && System.IO.File.Exists(_selectedLocalImagePath))
                {
                    // Upload ảnh lên Supabase Storage và lấy URL
                    var uploadedUrl = await _supabaseService.ReplaceAvatarAsync(_userSession.UserId, _selectedLocalImagePath);

                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        finalAvatarUrl = uploadedUrl;
                        System.Diagnostics.Debug.WriteLine($"[EditProfile] Avatar uploaded: {uploadedUrl}");
                    }
                    else
                    {
                        MessageBox.Show("Không thể upload ảnh lên server. Vui lòng thử lại.", "Lỗi Upload");
                        return;
                    }
                }

                // Cập nhật display_name lên Supabase (nếu có thay đổi)
                if (_userSession.UserName != UserName || _userSession.AvatarURL != finalAvatarUrl)
                {
                    await _supabaseService.UpdateProfileAsync(_userSession.UserId, UserName, finalAvatarUrl);
                }

                // Cập nhật lại Session cục bộ để hiển thị ngay
                _userSession.UpdateUserInfo(UserName, finalAvatarUrl);
                AvatarURL = finalAvatarUrl;

                MessageBox.Show($"Đã lưu thành công!\nTên mới: {UserName}", "Thông báo");

                // Reset đường dẫn tạm
                _selectedLocalImagePath = null;

                // Đóng cửa sổ sau khi lưu thành công
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditProfile] Error: {ex.Message}");
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            // 1. Khôi phục lại dữ liệu gốc (Hoàn tác những gì vừa gõ)
            LoadUserData();

            // 2. Gọi hành động đóng cửa sổ
            CloseAction?.Invoke();
        }
    }
}
