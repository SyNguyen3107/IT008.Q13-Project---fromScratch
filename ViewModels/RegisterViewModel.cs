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
    public partial class RegisterViewModel : ObservableObject
    {
        // Thuộc tính Email - Tự động sinh ra public property Email
        [ObservableProperty]
        private string email;

        // Thuộc tính Password - Tự động sinh ra public property "Password"
        [ObservableProperty]
        private string password;

        // Xác nhận mật khẩu
        [ObservableProperty]
        private string confirmPassword;

        public RegisterViewModel()
        {
            // Constructor
        }

        // Command
        [RelayCommand]
        private async Task RegisterAsync(object parameter)
        {
            if (parameter is PasswordBox passwordBox)
            {
                string password = passwordBox.Password;
                // Việc so sánh ConfirmPassword nên được xử lý ở View hoặc truyền thêm tham số.
                // Để đơn giản, ViewModel này nhận mật khẩu chính để xử lý đăng ký.
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Please fill all fields", "Error");
                    return;
                }
                MessageBox.Show($"Register Request:\nEmail: {Email}", "Success");
            }
            await Task.CompletedTask;
        }
    }
}
