using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace EasyFlips.ViewModels
{
    public partial class OtpViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        [ObservableProperty] private string otp1; 
        [ObservableProperty] private string otp2; 
        [ObservableProperty] private string otp3; 
        [ObservableProperty] private string otp4; 
        [ObservableProperty] private string otp5; 
        [ObservableProperty] private string otp6;
        [ObservableProperty] private string otp7;
        [ObservableProperty] private string otp8;
        private readonly string _email;

        public string OtpCode { get; set; }

        public OtpViewModel(IAuthService authService, INavigationService navigationService, string email)
        {
            _authService = authService;
            _navigationService = navigationService;
            _email = email;
        }

        [RelayCommand]
        private async Task VerifyOtp()
        {
            
             OtpCode = $"{Otp1}{Otp2}{Otp3}{Otp4}{Otp5}{Otp6}{Otp7}{Otp8}";
            var success = await _authService.VerifyOtpAsync(_email, OtpCode);
            if (success)
            {
                MessageBox.Show("Xác thực OTP thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                //Mở ResetPasswordWindow
                _navigationService.ShowResetPasswordWindow();
                CloseCurrentWindow();

            }
            else
            {
                MessageBox.Show("Mã OTP không hợp lệ hoặc đã hết hạn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private void Cancel()
        {
            CloseCurrentWindow();

        }
        

    }
}
