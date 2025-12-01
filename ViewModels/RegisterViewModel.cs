using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
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
                ErrorMessage = "Please enter a valid password.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please fill in all fields.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            try
            {
                var userId = await _authService.RegisterAsync(Email, Password);

                if (!string.IsNullOrEmpty(userId))
                {
                    MessageBox.Show("Account created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    _navigationService.ShowLoginWindow();

                    // Đóng cửa sổ hiện tại
                    CloseCurrentWindow();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message; // Hiện text đỏ trên UI
            }
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