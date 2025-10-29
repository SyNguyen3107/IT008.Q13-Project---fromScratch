using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.ViewModels;
using System.Windows;

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
            Loaded += async (s, e) => await _viewModel.LoadDecksAsync();

        }      
        private async void MainAnkiWindowLoaded(object sender, RoutedEventArgs e)
        {
            // 1. Lấy "bộ não" (ViewModel) từ DataContext
            if (DataContext is MainAnkiViewModel viewModel)
            {
                // 2. Ra lệnh cho nó tải dữ liệu
                await viewModel.LoadDecksAsync();
            }
        }
    }
}

