using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services; 
using Microsoft.Win32;    // Dùng cho OpenFileDialog của WPF
using System.Windows;    

namespace EasyFlips.ViewModels
{
    public record UserUpdatedMessage(string NewName, string NewAvatar);

    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;
        // Hành động để báo cho View biết là cần đóng cửa sổ (dùng cho nút Cancel)
        public Action CloseAction { get; set; }

        // Cần Client để gọi lệnh Update lên Server
        private readonly Supabase.Client _supabaseClient;


        [ObservableProperty] private string userName;
        [ObservableProperty] private string email;
        [ObservableProperty] private string avatarURL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;
        public bool IsNotBusy => !IsBusy;

        // Biến này để lưu đường dẫn file ảnh trên máy tính (để upload sau này)
        private string _selectedLocalImagePath;
        // Constructor: Nhận vào UserSession để lấy dữ liệu hiện tại
        public EditProfileViewModel(UserSession userSession)
        {
            _userSession = userSession;
            // _authService = authService; 

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
                // Hãy gán dữ liệu giả để không bị trống
                Email = "demo@easyflips.com";
            }
            _selectedLocalImagePath = null; // Reset ảnh chọn tạm
        }

        [RelayCommand]
        public void ChangeAvatar()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Lưu đường dẫn file local để lát nữa upload
                _selectedLocalImagePath = openFileDialog.FileName;

                // 2. Hiển thị ngay lên giao diện (Preview)
                // WPF tự động hiển thị ảnh từ đường dẫn file cục bộ
                AvatarURL = _selectedLocalImagePath;
            }
        }

        [RelayCommand]
        public async Task SaveProfile()
        {
            if (IsBusy) return; // Chặn bấm liên tục
            IsBusy = true;

            try
            {
                // Cập nhật lại Session cục bộ để hiển thị ngay
                _userSession.UpdateUserInfo(UserName, AvatarURL);

                MessageBox.Show($"Đã lưu thành công!", "Thông báo");

                _selectedLocalImagePath = null;
            }
            catch (Exception ex)
            {
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
            LoadUserData();

            // 2. Gọi hành động đóng cửa sổ (Code-behind sẽ xử lý việc này)
            CloseAction?.Invoke();
        }
    }
}