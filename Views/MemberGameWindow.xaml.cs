using EasyFlips.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class MemberGameWindow : Window
    {
        // [QUAN TRỌNG] Cờ kiểm soát: Chỉ cho phép đóng khi bấm nút hợp lệ
        private bool _canClose = false;

        public MemberGameWindow()
        {
            InitializeComponent();

            // Lắng nghe DataContext để gắn kết Action đóng cửa sổ khi NavigationService gán ViewModel
            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is MemberGameViewModel vm)
                {
                    vm.CloseWindowAction = () =>
                    {
                        _canClose = true; // Mở khóa cho phép đóng
                        this.Close();     // Thực hiện đóng cửa sổ
                    };
                }
            };
        }

        // [CHẶN NÚT X] Bắt sự kiện đang đóng cửa sổ
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_canClose)
            {
                e.Cancel = true; // Hủy lệnh đóng từ nút X hoặc Alt+F4
                MessageBox.Show("Please use the 'LEAVE ROOM' button to exit properly.",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            base.OnClosing(e);
        }

        // Đảm bảo dọn dẹp và mở lại MainWindow sau khi cửa sổ này thực sự đóng
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Hủy đăng ký action để tránh rò rỉ bộ nhớ
            if (this.DataContext is MemberGameViewModel vm)
            {
                vm.CloseWindowAction = null;
            }

            // Tìm và hiển thị lại sảnh chính (MainWindow)
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                mainWindow.Show();
                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;
            }
        }
    }
}