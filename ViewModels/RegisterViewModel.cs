using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string email;

        // Username
        [ObservableProperty]
        private string userName;

        // Lưu ý: Đang dùng Binding (Option B) cho Password ở Register
        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string confirmPassword;

        [ObservableProperty]
        private string errorMessage;

        public RegisterViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ErrorMessage = "";

            // Kiểm tra placeholder
            if (Password == "Enter Password" || ConfirmPassword == "Confirm Password")
            {
                ShowErrorDialog("Vui lòng nhập mật khẩu hợp lệ!");
                return;
            }

            // Kiểm tra trường trống
            if (string.IsNullOrWhiteSpace(UserName) && string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Email và mật khẩu không được để trống. Vui lòng nhập đầy đủ thông tin!");
                return;
            }

            if (string.IsNullOrWhiteSpace(UserName))
            {
                ShowErrorDialog("Vui lòng nhập tên hiển thị!");
                return;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ShowErrorDialog("Email không được để trống. Vui lòng nhập email của bạn!");
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Mật khẩu không được để trống. Vui lòng nhập mật khẩu của bạn!");
                return;
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Vui lòng xác nhận lại mật khẩu của bạn!");
                return;
            }

            // Kiểm tra mật khẩu có khớp không
            if (Password != ConfirmPassword)
            {
                ShowErrorDialog("Mật khẩu xác nhận không khớp. Vui lòng kiểm tra lại!");
                return;
            }

            // Kiểm tra độ dài mật khẩu
            if (Password.Length < 6)
            {
                ShowErrorDialog("Mật khẩu phải có ít nhất 6 ký tự. Vui lòng chọn mật khẩu mạnh hơn!");
                return;
            }

            try
            {
                var userId = await _authService.RegisterAsync(Email, Password, UserName);

                if (!string.IsNullOrEmpty(userId))
                {
                    MessageBox.Show("Tạo tài khoản thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                    _navigationService.ShowLoginWindow();

                    // Đóng cửa sổ hiện tại
                    CloseCurrentWindow();
                }
            }
            catch (Exception ex)
            {
                // Xử lý các loại lỗi khác nhau từ Firebase
                string errorMessage = GetUserFriendlyErrorMessage(ex);
                ShowErrorDialog(errorMessage);
            }
        }

        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            string exceptionMessage = ex.Message.ToUpper();
            
            // Lỗi email đã tồn tại
            if (exceptionMessage.Contains("EMAIL_EXISTS") || 
                exceptionMessage.Contains("EMAIL_ALREADY_IN_USE") ||
                exceptionMessage.Contains("ALREADY_EXISTS"))
            {
                return "Email này đã được đăng ký. Vui lòng sử dụng email khác hoặc đăng nhập nếu bạn đã có tài khoản!";
            }
            
            // Lỗi định dạng email không hợp lệ
            if (exceptionMessage.Contains("INVALID_EMAIL"))
            {
                return "Địa chỉ email không hợp lệ. Vui lòng kiểm tra lại định dạng email!";
            }
            
            // Lỗi mật khẩu yếu
            if (exceptionMessage.Contains("WEAK_PASSWORD"))
            {
                return "Mật khẩu quá yếu. Vui lòng chọn mật khẩu mạnh hơn (ít nhất 6 ký tự)!";
            }
            
            // Lỗi mạng
            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối internet của bạn!";
            }
            
            // Lỗi quá nhiều yêu cầu
            if (exceptionMessage.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
            {
                return "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau!";
            }
            
            // Các lỗi khác
            return "Đã xảy ra lỗi khi tạo tài khoản. Vui lòng thử lại sau!";
        }

        private void ShowErrorDialog(string message)
        {
            var errorDialog = new ErrorDialogWindow(message);
            errorDialog.ShowDialog();
        }

        [RelayCommand]
        private void BackToLogin()
        {
            _navigationService.ShowLoginWindow();
            CloseCurrentWindow();
        }

        private void CloseCurrentWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
        [ObservableProperty]
        private bool isPasswordVisible; // mặc định false
        [ObservableProperty]
        private bool isConfirmPasswordVisible;

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        [RelayCommand]
        private void ToggleConfirmPasswordVisibility()
        {
            IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
        }
    }
}