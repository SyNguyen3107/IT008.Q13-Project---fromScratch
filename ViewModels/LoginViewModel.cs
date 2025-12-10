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
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private bool isRememberMe = true;

        [ObservableProperty]
        private bool isPasswordVisible;

        public LoginViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;

            // [FIX]: Tự động điền Email và tick RememberMe nếu có lưu trong Settings
            // Giúp người dùng không phải gõ lại email nếu lần trước Auto-Login thất bại
            if (!string.IsNullOrEmpty(Settings.Default.UserEmail))
            {
                Email = Settings.Default.UserEmail;
                IsRememberMe = true;
            }
        }

        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is PasswordBox pwBox)
                Password = pwBox.Password;

            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Vui lòng nhập đầy đủ Email và Mật khẩu!");
                return;
            }

            try
            {
                bool isSuccess = await _authService.LoginAsync(Email, Password);

                if (isSuccess)
                {
                    string userId = _authService.CurrentUserId;

                    if (IsRememberMe)
                    {
                        // Lưu thông tin để lần sau Auto-Login
                        Settings.Default.UserId = userId;
                        Settings.Default.UserEmail = Email;
                        Settings.Default.UserToken = userId;
                        Settings.Default.Save();
                    }
                    else
                    {
                        // Nếu không chọn Remember Me, xóa sạch settings
                        Settings.Default.UserId = string.Empty;
                        Settings.Default.UserToken = string.Empty;
                        Settings.Default.UserEmail = string.Empty;
                        Settings.Default.Save();
                    }

                    _navigationService.ShowMainWindow();
                    CloseCurrentWindow();
                }
                else
                {
                    ShowErrorDialog("Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin!");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = GetUserFriendlyErrorMessage(ex);
                ShowErrorDialog(errorMessage);
            }
        }

        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            string exceptionMessage = ex.Message.ToUpper();

            if (exceptionMessage.Contains("INVALID_PASSWORD") ||
                exceptionMessage.Contains("INVALID_LOGIN_CREDENTIALS") ||
                exceptionMessage.Contains("USER_NOT_FOUND") ||
                exceptionMessage.Contains("EMAIL_NOT_FOUND") ||
                exceptionMessage.Contains("INVALID_EMAIL"))
            {
                return "Thông tin đăng nhập không chính xác!";
            }

            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Lỗi kết nối mạng!";
            }

            return "Đã xảy ra lỗi: " + ex.Message;
        }

        private void ShowErrorDialog(string message)
        {
            var errorDialog = new ErrorDialogWindow(message);
            errorDialog.ShowDialog();
        }

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