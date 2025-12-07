using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class TestRealtimeWindow : Window
    {
        public TestRealtimeWindow()
        {
            InitializeComponent();
            // Mỗi cửa sổ Test sẽ có một ViewModel riêng -> Một RealtimeService riêng
            // -> Hoạt động như 2 máy tính khác nhau
            DataContext = new TestRealtimeViewModel();
        }

        // Sự kiện mở cửa sổ mới
        private void OpenNewWindow_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new TestRealtimeWindow();
            newWindow.Show();
        }
    }
}