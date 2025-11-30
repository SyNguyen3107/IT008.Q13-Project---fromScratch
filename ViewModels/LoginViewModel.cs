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
                if (Password == "Enter Password") Password = "";
            }

            ErrorMessage = string.Empty; // Reset lỗi trước mỗi lần đăng nhập

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email và mật khẩu không được để trống";
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
                ErrorMessage = ex.Message; // Hiển thị lỗi lên UI thay vì MessageBox
            }
        }
    }
}