using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Properties;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyFlips.ViewModels
{
    // Bắt buộc có 'partial' để Toolkit sinh code ngầm
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        // Thuộc tính hiển thị lỗi
        [ObservableProperty]
        private string errorMessage; // Rỗng nếu không có lỗi

        // Thêm thuộc tính cho Checkbox
        [ObservableProperty]
        private bool isRememberMe = true; // Mặc định là tick chọn

        // Thuộc tính cho việc hiển thị/mật khẩu

        [ObservableProperty]
        private bool isPasswordVisible; // mặc định false

        public LoginViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
        }

        // Tự động sinh ra lệnh: LoginCommand (đã bỏ chữ Async)
        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is PasswordBox pwBox)
                Password = pwBox.Password;

            ErrorMessage = "";

            // Kiểm tra email và mật khẩu trống
            if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Email và mật khẩu không được để trống. Vui lòng nhập đầy đủ thông tin!");
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

            try
            {
                string userId = await _authService.LoginAsync(Email, Password);

                if (IsRememberMe)
                {
                    Settings.Default.UserId = userId;
                    Settings.Default.UserEmail = Email;
                    Settings.Default.UserToken = _authService.CurrentUserId;
                    Settings.Default.Save();
                }
                else
                {
                    Settings.Default.UserId = string.Empty;
                    Settings.Default.UserToken = string.Empty;
                    Settings.Default.UserEmail = string.Empty;
                    Settings.Default.Save();
                }

                _navigationService.ShowMainWindow();
                CloseCurrentWindow();
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
            
            // Lỗi thông tin đăng nhập không chính xác
            if (exceptionMessage.Contains("INVALID_PASSWORD") || 
                exceptionMessage.Contains("INVALID_LOGIN_CREDENTIALS") ||
                exceptionMessage.Contains("USER_NOT_FOUND") ||
                exceptionMessage.Contains("EMAIL_NOT_FOUND") ||
                exceptionMessage.Contains("INVALID_EMAIL"))
            {
                return "Thông tin đăng nhập không chính xác. Vui lòng kiểm tra lại email hoặc mật khẩu và thử lại!";
            }
            
            // Lỗi tài khoản bị vô hiệu hóa
            if (exceptionMessage.Contains("USER_DISABLED"))
            {
                return "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ với quản trị viên!";
            }
            
            // Lỗi quá nhiều lần đăng nhập thất bại
            if (exceptionMessage.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
            {
                return "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau!";
            }
            
            // Lỗi mạng
            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối internet của bạn!";
            }
            
            // Các lỗi khác
            return "Đã xảy ra lỗi khi đăng nhập. Vui lòng thử lại sau!";
        }

        private void ShowErrorDialog(string message)
        {
            var errorDialog = new ErrorDialogWindow(message);
            errorDialog.ShowDialog();
        }

        // Tự động sinh ra lệnh: OpenRegisterCommand
        [RelayCommand]
        private void OpenRegister()
        {
            _navigationService.ShowRegisterWindow();
            CloseCurrentWindow();
        }

        private void CloseCurrentWindow()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }
    }
}