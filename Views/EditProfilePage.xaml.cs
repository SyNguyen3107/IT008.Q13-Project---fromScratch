using System.Windows;
using System.Windows.Controls;
using EasyFlips.ViewModels;
using EasyFlips.Services;
using Microsoft.Extensions.DependencyInjection; // Cần dòng này để dùng GetRequiredService

namespace EasyFlips.Views
{
    public partial class EditProfilePage : Page
    {
        public EditProfilePage(UserSession session)
        {
            InitializeComponent();

            if (session != null)
            {
                // 1. Lấy Supabase Client trực tiếp từ thùng chứa Service của App
                // (Không cần truyền từ MainWindow sang nữa)
                var supabaseClient = EasyFlips.App.ServiceProvider.GetRequiredService<Supabase.Client>();

                // 2. Truyền cả Session và Client vào ViewModel
                // (Đảm bảo ViewModel đã cập nhật Constructor nhận 2 tham số như hướng dẫn trước)
                var viewModel = new EditProfileViewModel(session, supabaseClient);

                // 3. Setup nút Cancel/Close
                viewModel.CloseAction = () =>
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null && mainWindow.MainFrame != null)
                    {
                        mainWindow.MainFrame.Visibility = Visibility.Hidden;
                        mainWindow.MainFrame.Content = null;
                    }
                };

                this.DataContext = viewModel;
            }
        }
    }
}