using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.ViewModels;
using System.Windows;
using IT008.Q13_Project___fromScratch.Models;
using System.Windows.Controls;

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
    }
}

