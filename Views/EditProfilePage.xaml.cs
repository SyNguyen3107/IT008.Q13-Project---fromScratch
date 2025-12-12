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
using Supabase.Gotrue;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

using EasyFlips.Services;

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for EditProfilePage.xaml
    /// </summary>
    public partial class EditProfilePage : Page
    {
        public EditProfilePage()
        {
            InitializeComponent();
            // [FIX] Lấy UserSession từ DI container thay vì tạo mới
            var session = App.ServiceProvider.GetRequiredService<UserSession>();
            var viewModel = new EditProfileViewModel(session);

            // THÊM ĐOẠN NÀY:
            viewModel.CloseAction = () =>
            {// 1. Tìm cửa sổ chính (MainWindow) đang chạy
                var mainWindow = Application.Current.MainWindow as MainWindow;

                // 2. Nếu tìm thấy, hãy ẩn cái Frame đi
                if (mainWindow != null && mainWindow.MainFrame != null)
                {
                    mainWindow.MainFrame.Visibility = Visibility.Hidden;

                    // (Tùy chọn) Xóa nội dung trang Edit đi để giải phóng bộ nhớ
                    mainWindow.MainFrame.Content = null;
                }
            };

            this.DataContext = viewModel;
        }
    }
}
