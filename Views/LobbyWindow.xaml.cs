using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class LobbyWindow : Window
    {
        // [QUAN TRỌNG] Inject ViewModel vào Constructor
        public LobbyWindow(LobbyViewModel viewModel)
        {
            InitializeComponent();

            // Gán DataContext để giao diện biết lấy dữ liệu ở đâu
            DataContext = viewModel;
        }

        // Chặn người dùng đóng cửa sổ bằng nút X nếu chưa được phép
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is LobbyViewModel vm)
            {
                // Nếu vm.CanCloseWindow = false (đang trong phòng), chặn đóng
                if (!vm.CanCloseWindow)
                {
                    e.Cancel = true;
                    MessageBox.Show("Vui lòng dùng nút 'Rời phòng' hoặc 'Giải tán' bên trong giao diện.", "Cảnh báo");
                }
            }
        }
    }
}