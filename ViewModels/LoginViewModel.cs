using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Properties;
using EasyFlips.Services;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace EasyFlips.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly Supabase.Client _supabaseClient;
        private readonly UserSession _userSession;

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

        public LoginViewModel(IAuthService authService, INavigationService navigationService, Supabase.Client client, UserSession userSession)
        {
            _authService = authService;
            _navigationService = navigationService;
            _supabaseClient = client;
            _userSession = userSession;

            if (!string.IsNullOrEmpty(Settings.Default.UserEmail))
            {
                Email = Settings.Default.UserEmail;
                IsRememberMe = true;
            }
        }

        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is PasswordBox pwBox) Password = pwBox.Password;
            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Please enter your email and password.");
                return;
            }

            try
            {
                
                bool isSuccess = await _authService.LoginAsync(Email, Password);

                if (isSuccess)
                {
                    
                    if (IsRememberMe)
                    {
                        Settings.Default.UserId = _userSession.UserId;
                        Settings.Default.UserEmail = _userSession.Email;
                        Settings.Default.RefreshToken = _userSession.RefreshToken; 
                        Settings.Default.Save();
                    }
                    else
                    {
                        Settings.Default.UserId = string.Empty;
                        Settings.Default.UserToken = string.Empty;
                        Settings.Default.RefreshToken = string.Empty;
                        Settings.Default.UserEmail = string.Empty;
                        Settings.Default.Save();
                    }
                    }
                _navigationService.ShowMainWindow();
                CloseCurrentWindow();
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException ex)
            {
                ShowErrorDialog(GetUserFriendlyErrorMessage(ex));
            }
                
            
            catch (Exception ex)
            {
                ShowErrorDialog("An error occurred: " + ex.Message);
            }
        
}


        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            string exceptionMessage = ex.Message.ToUpper();

            if (exceptionMessage.Contains("INVALID_PASSWORD") ||
                exceptionMessage.Contains("INVALID_CREDENTIALS") ||
                exceptionMessage.Contains("USER_NOT_FOUND") ||
                exceptionMessage.Contains("EMAIL_NOT_FOUND") ||
                exceptionMessage.Contains("INVALID_EMAIL"))
            {
                return "Invalid login credentials!";
            }

            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Network connection error!";
            }

            return "An error occurred: " + ex.Message;
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

        [RelayCommand]
        private async Task ForgotPassword()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                MessageBox.Show("Please enter your email.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
               $"Are you sure you want to send the OTP to {Email}?",
            "Confirm",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            var success = await _authService.SendOtpAsync(Email);
            if (success)
            {
                MessageBox.Show("An OTP has been sent to your email.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                var otpWindow = new OtpWindow
                {
                    DataContext = new OtpViewModel(_authService, _navigationService, Email)
                };
                otpWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Failed to send OTP. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}