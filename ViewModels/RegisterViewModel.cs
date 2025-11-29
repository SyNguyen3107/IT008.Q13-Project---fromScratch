using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject
using CommunityToolkit.Mvvm.Input; // Relay Command
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EasyFlips.Services;
using EasyFlips.Views;
using EasyFlips.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFlips.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        // Thuộc tính Email - Tự động sinh ra public property Email
        [ObservableProperty]
        private string email;

        // Thuộc tính Password - Tự động sinh ra public property "Password"
        [ObservableProperty]
        private string password;

        // Xác nhận mật khẩu
        [ObservableProperty]
        private string confirmPassword;

        [ObservableProperty]
        private string errorMessage;

        public RegisterViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ErrorMessage = string.Empty;

            // 1. Kiểm tra nhập đủ
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please fill in all fields.";
                return;
            }

            // 2. Kiểm tra password trùng khớp
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            try
            {
                // 3. Gọi Firebase đăng ký
                var userId = await _authService.RegisterAsync(Email, Password);

                if (!string.IsNullOrEmpty(userId))
                {
                    // 4. Sau khi đăng ký xong → Quay lại LoginWindow
                    var loginWindow = App.ServiceProvider.GetService<LoginWindow>();
                    loginWindow.Show();

                    // Đóng RegisterWindow hiện tại
                    var registerWindow = Application.Current.Windows.OfType<RegisterWindow>().FirstOrDefault();
                    registerWindow?.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void BackToLogin()
        {
            // Chuyển sang LoginWindow
            var loginWindow = App.ServiceProvider.GetService<LoginWindow>();
            loginWindow.Show();

            // Đóng RegisterWindow hiện tại
            var registerWindow = Application.Current.Windows.OfType<RegisterWindow>().FirstOrDefault();
            registerWindow?.Close();
        }
    }
}
