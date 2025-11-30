using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EasyFlips.Interfaces;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFlips.ViewModels
{
    // BẮT BUỘC PHẢI CÓ 'partial'
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        public LoginViewModel(IAuthService authService, INavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
        }

        // --- DÙNG LẠI RELAY COMMAND ---

        [RelayCommand] // -> Tự sinh ra: LoginCommand (đã cắt chữ Async)
        private async Task LoginAsync(object parameter)
        {
            // Code xử lý giữ nguyên
            if (parameter is PasswordBox pwBox)
            {
                Password = pwBox.Password;
                if (Password == "Enter Password") Password = "";
            }

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || Email == "Enter Email")
            {
                MessageBox.Show("Vui lòng nhập Email và Mật khẩu!", "Cảnh báo");
                return;
            }

            try
            {
                string userId = await _authService.LoginAsync(Email, Password);

                var main = App.ServiceProvider.GetRequiredService<MainWindow>();
                main.Show();

                // Đóng LoginWindow
                var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                window?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi Đăng Nhập", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand] // -> Tự sinh ra: OpenRegisterCommand
        private void OpenRegister()
        {
            _navigationService.ShowRegisterWindow();

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
    }
}