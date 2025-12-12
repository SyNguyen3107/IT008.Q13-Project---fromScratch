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
            // ... code khởi tạo session ...
            var viewModel = new EditProfileViewModel(session);

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
