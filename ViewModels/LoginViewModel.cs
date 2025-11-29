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
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        // Thuộc tính Email - Tự động sinh ra public property Email
        [ObservableProperty]
        private string email;

        // Thuộc tính Password - Tự động sinh ra public property "Password"
        [ObservableProperty]
        private string password;

        // Inject AuthService
        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        // Command Login 
        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is PasswordBox pwBox)
                Password = pwBox.Password;

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Email và mật khẩu không được để trống!");
                return;
            }

            try
            {
                string userId = await _authService.LoginAsync(Email, Password);

                // LẤY MainWindow từ DI
                var main = App.ServiceProvider.GetRequiredService<MainWindow>();
                main.Show();

                // Đóng cửa sổ login hiện tại
                var loginWindow = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this);

                loginWindow?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Đăng nhập thất bại");
            }
        }
    }
}
