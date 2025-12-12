using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Services; 
using Microsoft.Win32;    // Dùng cho OpenFileDialog của WPF
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;

namespace EasyFlips.ViewModels
{
    // Tạo một tin nhắn chứa thông tin cập nhật (Bạn có thể để class này ở file riêng hoặc cuối file này)
    public record UserUpdatedMessage(string NewName, string NewAvatar);
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserSession _userSession;
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
            // Mở hộp thoại chọn file của Windows
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";

            if (openFileDialog.ShowDialog() == true)
            {
                // 1. Lưu đường dẫn file để tí nữa upload
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
                WeakReferenceMessenger.Default.Send(new UserUpdatedMessage(UserName, AvatarURL));
                MessageBox.Show($"Đã lưu thành công!\nTên mới: {UserName}", "Thông báo");

                // Reset đường dẫn tạm
                _selectedLocalImagePath = null;
                CloseAction?.Invoke();
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
            // 1. Khôi phục lại dữ liệu gốc (Hoàn tác những gì vừa gõ)
            LoadUserData();

            // 2. Gọi hành động đóng cửa sổ (Code-behind sẽ xử lý việc này)
            CloseAction?.Invoke();
        }
    }
}
