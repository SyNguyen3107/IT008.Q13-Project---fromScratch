using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class RegisterWindow : Window
    {
        // Constructor injection: Nhận ViewModel từ DI
        public RegisterWindow(RegisterViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // XÓA HẾT các logic xử lý Placeholder thủ công ở đây.
            // WatermarkService trong XAML sẽ tự động lo việc đó.
        }
    }
}