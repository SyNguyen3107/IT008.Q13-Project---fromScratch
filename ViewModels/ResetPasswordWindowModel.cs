using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Views;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class ResetPasswordViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty] private string newPassword;
        [ObservableProperty] private string confirmPassword;
        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private bool isPasswordVisible;
        [ObservableProperty] private bool isConfirmPasswordVisible;

        public ResetPasswordViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
        }
        [RelayCommand]
        private async Task Test()
        {
            ErrorMessage = "";
            System.Diagnostics.Debug.WriteLine("ResetPasswordAsync called");
            if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ShowErrorDialog("Passwords do not match.");
                return;
            }

            if (NewPassword.Length < 6)
            {
                ShowErrorDialog("Password must be at least 6 characters.");
                return;
            }
            try
            {
                bool isSuccess = await _authService.UpdatePasswordAsync(NewPassword);

                if (isSuccess)
                {
                    MessageBox.Show("Password updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseCurrentWindow();
                }
                else
                {
                    ShowErrorDialog("Failed to update password. Please try again.");
                }
            }
            catch (Exception ex)
            {
                string userFriendlyMessage = GetUserFriendlyErrorMessage(ex);
                ShowErrorDialog(userFriendlyMessage);
            }
        }

        [RelayCommand]
        private async Task ResetPasswordAsync()
        {
            ErrorMessage = "";
            System.Diagnostics.Debug.WriteLine("ResetPasswordAsync called");
            if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ShowErrorDialog("Please fill in all fields!");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ShowErrorDialog("Passwords do not match.");
                return;
            }

            if (NewPassword.Length < 6)
            {
                ShowErrorDialog("Password must be at least 6 characters.");
                return;
            }
            
            //try
            //{
            //    bool isSuccess = await _authService.UpdatePasswordAsync(NewPassword);

            //    if (isSuccess)
            //    {
            //        MessageBox.Show("Password updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            //        _navigationService.ShowLoginWindow();
            //        CloseCurrentWindow();
            //    }
            //    else
            //    {
            //        ShowErrorDialog("Failed to update password. Please try again.");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    string userFriendlyMessage = GetUserFriendlyErrorMessage(ex);
            //    ShowErrorDialog(userFriendlyMessage);
            //}
        }

        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            string msg = ex.Message.ToUpper();
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
