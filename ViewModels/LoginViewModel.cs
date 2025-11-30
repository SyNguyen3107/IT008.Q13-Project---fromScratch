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
    // Bắt buộc có 'partial' để Toolkit sinh code ngầm
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

        // Tự động sinh ra lệnh: LoginCommand (đã bỏ chữ Async)
        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            // 1. Lấy Password từ View (PasswordBox)
            if (parameter is PasswordBox pwBox)
            {
                Password = pwBox.Password;
                // Xử lý placeholder
                if (Password == "Enter Password") Password = "";
            }

            // 2. Validate
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || Email == "Enter Email")
            {
                MessageBox.Show("Vui lòng nhập Email và Mật khẩu!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 3. Gọi Service Đăng nhập
                string userId = await _authService.LoginAsync(Email, Password);

                MessageBox.Show($"Đăng nhập thành công!\nUser ID: {userId}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. Mở MainWindow
                var main = App.ServiceProvider.GetRequiredService<MainWindow>();
                main.Show();

                // 5. Đóng LoginWindow hiện tại
                CloseCurrentWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Đăng nhập thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
}