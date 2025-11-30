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

        // Thuộc tính hiển thị lỗi
        [ObservableProperty]
        private string errorMessage; // Rỗng nếu không có lỗi

        // Inject AuthService
        public LoginViewModel(IAuthService authService)
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

            ErrorMessage = string.Empty; // Reset lỗi trước mỗi lần đăng nhập

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email và mật khẩu không được để trống";
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
                ErrorMessage = ex.Message; // Hiển thị lỗi lên UI thay vì MessageBox
            }
        }
    }
}