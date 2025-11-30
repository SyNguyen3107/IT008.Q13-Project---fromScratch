using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EasyFlips.ViewModels;

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        // ✅ INJECT LoginViewModel
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel; 
            
            // ✅ Khởi tạo placeholder cho Email
            txtEmail.Text = "Enter Email";
            txtEmail.Foreground = Brushes.Gray;
            
            txtPassword.Password = (string)txtPassword.Tag;
        }

        private void Password_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtPassword.Password == (string)txtPassword.Tag)
            {
                //Xóa placeholder khi textbox được focus
                txtPassword.Password = string.Empty;
                txtPassword.Foreground = Brushes.White;
            }
        }

        private void Password_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                //Khôi phục placeholder khi textbox mất focus và không có nội dung
                txtPassword.Password = (string)txtPassword.Tag;
                txtPassword.Foreground = Brushes.Gray;
            }
        }
        
        private void Email_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtEmail.Text == "Enter Email")
            {
                //Xóa placeholder khi textbox được focus
                txtEmail.Text = string.Empty;
                txtEmail.Foreground = Brushes.White;
            }
        }

        private void Email_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                //Khôi phục placeholder khi textbox mất focus và không có nội dung
                txtEmail.Text = "Enter Email";
                txtEmail.Foreground = Brushes.Gray;
            }
        }
        
        
    }
}
