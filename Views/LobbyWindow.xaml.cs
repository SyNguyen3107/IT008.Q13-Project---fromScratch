using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class LobbyWindow : Window
    {
        // Biến lưu trữ cục bộ (nếu cần dùng sau này)
        private readonly int _maxPlayers;

        // [FIX]: Thêm tham số maxPlayers vào Constructor
        public LobbyWindow(LobbyViewModel viewModel, int maxPlayers = 30)
        {
            InitializeComponent();

            _maxPlayers = maxPlayers;

            // [QUAN TRỌNG]: Truyền giá trị MaxPlayers vào ViewModel để Binding ra UI
            // Lưu ý: Bạn cần chắc chắn LobbyViewModel đã có property 'MaxPlayers'
            if (viewModel != null)
            {
                viewModel.MaxPlayers = _maxPlayers;
            }

            // Gán DataContext
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