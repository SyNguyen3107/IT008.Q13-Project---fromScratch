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

        [ObservableProperty] private string userName;
        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
        [ObservableProperty] private string confirmPassword;
        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private bool isPasswordVisible;
        [ObservableProperty] private bool isConfirmPasswordVisible;

        public RegisterViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            if (Password != ConfirmPassword)
            {
                ShowErrorDialog("Passwords do not match.");
                return;
            }

            if (Password.Length < 6)
            {
                ShowErrorDialog("Password must be at least 6 characters.");
                return;
            }

            try
            {
                // [FIX]: Xử lý kết quả bool trả về từ RegisterAsync
                bool isSuccess = await _authService.RegisterAsync(Email, Password, UserName);

                if (isSuccess)
                {
                    MessageBox.Show("Successfully Registered!", "Account Created", MessageBoxButton.OK, MessageBoxImage.Information);

                    _navigationService.ShowLoginWindow();
                    CloseCurrentWindow();
                }
                else
                {
                    ShowErrorDialog("Registration failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                string userFriendlyMessage = GetUserFriendlyErrorMessage(ex);
                ShowErrorDialog(userFriendlyMessage);
            }
        }

        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            string msg = ex.Message.ToUpper();
            if (msg.Contains("EMAIL_EXISTS") || msg.Contains("ALREADY_IN_USE")) return "Email already exists!";
            if (msg.Contains("WEAK_PASSWORD")) return "Password is too weak!";
            if (msg.Contains("NETWORK")) return "Network error!";
            return $"Error: {ex.Message}";
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
        private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

        [RelayCommand]
        private void ToggleConfirmPasswordVisibility() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    }
}