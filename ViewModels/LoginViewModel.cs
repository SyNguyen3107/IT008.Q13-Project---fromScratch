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

        // Thêm Supabase Client để lấy chi tiết Session (RefreshToken)
        private readonly Supabase.Client _supabaseClient;

        // Thêm UserSession để cập nhật thông tin đăng nhập
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

        // Constructor nhận thêm SupabaseClient và UserSession
        public LoginViewModel(IAuthService authService, INavigationService navigationService, Supabase.Client client, UserSession userSession)
        {
            _authService = authService;
            _navigationService = navigationService;
            _supabaseClient = client;
            _userSession = userSession;

            // [FIX]: Tự động điền Email và tick RememberMe nếu có lưu trong Settings
            // Giúp người dùng không phải gõ lại email nếu lần trước Auto-Login thất bại
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

            // 1. Kiểm tra input
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorDialog("Vui lòng nhập đầy đủ Email và Mật khẩu!");
                return;
            }

            try
            {
                // 2. Gọi Service đăng nhập
                // Service này đã tự động cập nhật _userSession nếu thành công
                bool isSuccess = await _authService.LoginAsync(Email, Password);

                if (isSuccess)
                {
                    // [FIX QUAN TRỌNG]: XOÁ BỎ ĐOẠN CHECK SESSION CŨ
                    // Không cần gọi _supabaseClient.Auth.CurrentSession nữa (vì nó đang bị null do lỗi DI)

                    // 3. Lưu Settings (Lấy dữ liệu từ UserSession đã được Service nạp)
                    if (IsRememberMe)
                    {
                        Settings.Default.UserId = _userSession.UserId;
                        Settings.Default.UserEmail = _userSession.Email;
                        Settings.Default.RefreshToken = _userSession.RefreshToken; // Đảm bảo UserSession có trường này
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
                // Mở giao diện chính
                _navigationService.ShowMainWindow();
                CloseCurrentWindow();
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException ex)
            {
                ShowErrorDialog(GetUserFriendlyErrorMessage(ex));
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
                return "Thông tin đăng nhập không chính xác!";
            }

            if (exceptionMessage.Contains("NETWORK") || exceptionMessage.Contains("CONNECTION"))
            {
                return "Lỗi kết nối mạng!";
            }

            return "Đã xảy ra lỗi: " + ex.Message;
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
                MessageBox.Show("Vui lòng nhập email!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Hỏi xác nhận trước khi gửi OTP
            var result = MessageBox.Show(
                $"Bạn có chắc muốn gửi OTP tới email {Email}?",
                "Xác nhận",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.OK)
            {
                // Người dùng bấm Cancel → không gửi OTP
                return;
            }

            // Người dùng bấm OK → tiến hành gửi OTP
            var success = await _authService.SendOtpAsync(Email);
            if (success)
            {
                MessageBox.Show("Mã OTP đã được gửi tới email của bạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // Mở cửa sổ nhập OTP
                var otpWindow = new OtpWindow
                {
                    DataContext = new OtpViewModel(_authService, _navigationService, Email)
                };
                otpWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Không thể gửi OTP. Vui lòng thử lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}