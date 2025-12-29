using EasyFlips.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class HostGameWindow : Window
    {
        private bool _canClose = false;

        public HostGameWindow()
        {
            InitializeComponent();

            // Lắng nghe DataContext để gán Action đóng cửa sổ
            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is HostGameViewModel vm)
                {
                    vm.CloseWindowAction = () =>
                    {
                        _canClose = true; // Mở chốt an toàn
                        this.Close();     // Đóng cửa sổ
                    };
                }
            };
        }

        // CHỈ GIỮ LẠI LOGIC CHẶN NÚT X
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_canClose)
            {
                e.Cancel = true; // Chặn lại
                MessageBox.Show("Vui lòng nhấn nút 'QUIT GAME' để giải tán phòng đúng cách!",
                                "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            base.OnClosing(e);
        }

        // [ĐÃ XÓA] Logic OnClosed mở MainWindow -> Vì ViewModel đã lo việc này rồi!
    }
}