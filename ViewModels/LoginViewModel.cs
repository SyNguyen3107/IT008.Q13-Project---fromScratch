using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel; // Cần cho ObservableObject, [ObservableProperty]
using CommunityToolkit.Mvvm.Input; // Cần cho [RelayCommand]

namespace EasyFlips.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        // Thuộc tính Email - Tự động sinh ra public property Email
        [ObservableProperty]
        private string email;

        // Thuộc tính Password - Tự động sinh ra public property "Password"
        [ObservableProperty]
        private string password;

        public LoginViewModel()
        {
            // Constructor
        }

        // Command Login 
        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            // Xử lý lấy Password từ PasswordBox vì PasswordBox không cho phép binding trực tiếp vì lý do bảo mật
            if (parameter is PasswordBox passwordBox)
            {
                string password = passwordBox.Password;
                MessageBox.Show($"Login with:\nEmail: {Email}\nPassword Length: {password.Length}", "Login Info");
            }
            await Task.CompletedTask;
        }
    }
}
