using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.ViewModels;
using System.Windows;
using IT008.Q13_Project___fromScratch.Models;
using System.Windows.Controls;
using System.Windows.Input; // Cần cho MouseButtonEventArgs
using System.Windows.Media; // Cần cho VisualTreeHelper
using System.Windows.Controls.Primitives; // <-- Thêm để dùng ScrollBar
namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for MainAnkiWindow.xaml
    /// </summary>
    public partial class MainAnkiWindow : Window
    {
        private readonly MainAnkiViewModel _viewModel;
        // Hàm khởi tạo nhận vào ViewModel
        public MainAnkiWindow( MainAnkiViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            //Gọi hàm LoadDecksAsync khi cửa sổ được tải
            this.Loaded += MainAnkiWindowLoaded;

        }      
        private async void MainAnkiWindowLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadDecksAsync();
        }
        private void ListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listView = sender as System.Windows.Controls.ListView;
            var selectedDeck = listView?.SelectedItem as Models.Deck;
            if (selectedDeck != null)
            {
                _viewModel.ShowDeckChosenCommand.Execute(selectedDeck);
            }

        }
        private void GearButton_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem người gửi có phải là Button và có ContextMenu không
            if (sender is Button btn && btn.ContextMenu != null)
            {
                // Đặt mục tiêu hiển thị là chính cái nút đó
                btn.ContextMenu.PlacementTarget = btn;

                // Mở menu
                btn.ContextMenu.IsOpen = true;

                // Ngăn sự kiện click lan ra ngoài (tránh chọn nhầm dòng ListView bên dưới)
                e.Handled = true;
            }
        }
        // --- HÀM MỚI: XỬ LÝ BỎ CHỌN ---
        // Hàm này đủ thông minh để áp dụng cho bất kỳ container nào (Grid, Border, Window)
        private void ClearSelection_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 1. Nếu click chuột phải -> Không làm gì (để menu chuột phải hoạt động)
            if (e.ChangedButton != MouseButton.Left) return;

            var dependencyObject = (DependencyObject)e.OriginalSource;

            while (dependencyObject != null && dependencyObject != DeckListView)
            {
                // 2. Nếu click trúng Item trong list -> Để yên cho sự kiện chọn chạy
                if (dependencyObject is ListViewItem) return;

                // 3. Nếu click trúng Thanh cuộn (ScrollBar) -> Không bỏ chọn (để người dùng cuộn)
                if (dependencyObject is ScrollBar) return;

                // 4. Nếu click trúng Nút bấm, TextBox, v.v. -> Không bỏ chọn (để người dùng thao tác nút đó)
                if (dependencyObject is Button || dependencyObject is TextBox || dependencyObject is Menu) return;

                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            // Nếu chạy hết vòng lặp mà không trúng các thành phần trên -> Bỏ chọn
            DeckListView.SelectedItem = null;

            // (Tùy chọn) Làm mất focus khỏi ListView để giao diện sạch hơn
            Keyboard.ClearFocus(); 
        }
    }
}

