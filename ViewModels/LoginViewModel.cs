using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Properties;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        // Thuộc tính hiển thị lỗi
        [ObservableProperty]
        private string errorMessage; // Rỗng nếu không có lỗi

        // Thêm thuộc tính cho Checkbox
        [ObservableProperty]
        private bool isRememberMe = true; // Mặc định là tick chọn

        // Thuộc tính cho việc hiển thị/mật khẩu

        [ObservableProperty]
        private bool isPasswordVisible; // mặc định false

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
                Password = pwBox.Password;

            ErrorMessage = "";

            // Kiểm tra đầu vào (Validation)
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                return;
            }

            try
            {
                // 3. Call Login Service
                string userId = await _authService.LoginAsync(Email, Password);

                // HANDLE "REMEMBER ME"
                if (IsRememberMe)
                {
                    // Save to Properties.Settings
                    Settings.Default.UserId = userId;
                    Settings.Default.UserEmail = Email;

                    // Lưu ý: Đảm bảo bạn lấy đúng Token từ Session nếu muốn lưu Token
                    // Settings.Default.UserToken = _userSession.Token; 
                    // Tạm thời giữ nguyên logic của bạn:
                    Settings.Default.UserToken = _authService.CurrentUserId;

                    Settings.Default.Save();
                }
                else
                {
                    // Clear saved data
                    Settings.Default.UserId = string.Empty;
                    Settings.Default.UserToken = string.Empty;
                    Settings.Default.UserEmail = string.Empty;
                    Settings.Default.Save();
                }

                // 4. Open MainWindow
                _navigationService.ShowMainWindow();

                // 5. Close current LoginWindow
                CloseCurrentWindow();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message; // Hiển thị lỗi lên UI thay vì MessageBox
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

     
      
        

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }







    }
}