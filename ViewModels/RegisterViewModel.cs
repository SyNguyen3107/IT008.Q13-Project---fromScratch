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

        // Username
        [ObservableProperty]
        private string userName;

        [ObservableProperty]
        private string email;

        // Lưu ý: PasswordBox thường binding qua cơ chế khác (như PasswordBoxHelper hoặc tham số), 
        // nhưng ở đây tôi giữ nguyên pattern property nếu bạn đã dùng Helper
        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string confirmPassword;

        [ObservableProperty]
        private string errorMessage;

        // Thuộc tính hiển thị mật khẩu
        [ObservableProperty]
        private bool isPasswordVisible;

        [ObservableProperty]
        private bool isConfirmPasswordVisible;

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
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            // Kiểm tra trường trống
            if (string.IsNullOrWhiteSpace(UserName) && string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            if (string.IsNullOrWhiteSpace(UserName))
            {
                ShowErrorDialog("Please enter a Username!");
                return;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Please fill in all fields.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Please fill in all fields.");
                return;
            }

            // Kiểm tra mật khẩu có khớp không
            if (Password != ConfirmPassword)
            {
                ShowErrorDialog("Your passwords do not match.");
                return;
            }

            // Kiểm tra độ dài mật khẩu
            if (Password.Length < 6)
            {
                ShowErrorDialog("Password must be at least 6 characters long. Please choose a stronger password!");
                return;
            }

            try
            {
                var userId = await _authService.RegisterAsync(Email, Password, UserName);

                if (!string.IsNullOrEmpty(userId))
                {
                    if (userId == "CHECK_EMAIL")
                    {
                        MessageBox.Show("Registration successful! Please check your email to confirm your account.", "Check Email", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Successfully Registered!", "Account Created", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    _navigationService.ShowLoginWindow();

                    // Đóng cửa sổ hiện tại
                    CloseCurrentWindow();
                }
            }
            catch (Exception ex)
            {
                // Xử lý các loại lỗi khác nhau
                string userFriendlyMessage = GetUserFriendlyErrorMessage(ex);
                ShowErrorDialog(userFriendlyMessage);
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
                return "This email is already registered. Please use a different email or log in if you already have an account!";
            }

            // Lỗi định dạng email không hợp lệ
            if (exceptionMessage.Contains("INVALID_EMAIL"))
            {
                return "Invalid email address. Please check the email format!";
            }

            // Lỗi mật khẩu yếu
            if (exceptionMessage.Contains("WEAK_PASSWORD"))
            {
                return "Password is too weak. Please choose a stronger password (at least 6 characters)!";
            }

            // Lỗi mạng
            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Cannot connect to server. Please check your internet connection!";
            }

            // Lỗi quá nhiều yêu cầu
            if (exceptionMessage.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
            {
                return "Too many attempts. Please try again later!";
            }

            // Các lỗi khác
            return $"An error occurred while creating the account: {ex.Message}";
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