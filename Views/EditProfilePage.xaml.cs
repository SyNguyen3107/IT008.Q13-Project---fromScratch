using EasyFlips.Services;
using EasyFlips.ViewModels;
using Microsoft.Extensions.DependencyInjection; // Cần dòng này để dùng GetRequiredService
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;

namespace EasyFlips.Views
{
    public partial class EditProfilePage : Page
    {
        public EditProfilePage(EditProfileViewModel viewModel)
        {
            InitializeComponent();
            // ... code khởi tạo session ...


            this.DataContext = viewModel;

            // 3. Setup nút Cancel/Close
            viewModel.CloseAction = () =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.MainFrame != null)
                {
                    mainWindow.MainFrame.Content = null;
                    mainWindow.MainFrame.Visibility = Visibility.Hidden;
                }
            };
        }
        
    }
}
