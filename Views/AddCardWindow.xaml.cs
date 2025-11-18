using IT008.Q13_Project___fromScratch.ViewModels; // Thêm 'using' này
using System.Windows;

namespace IT008.Q13_Project___fromScratch.Views
{
    public partial class AddCardWindow : Window
    {
        private readonly AddCardViewModel _viewModel; // Thêm dòng này

        public AddCardWindow(AddCardViewModel viewModel) // Constructor nhận ViewModel
        {
            InitializeComponent();
            _viewModel = viewModel; // Thêm dòng này
            DataContext = _viewModel; // Thêm dòng này

            // Thêm 2 dòng này để tải danh sách Deck
            this.Loaded += AddCardWindow_Loaded;
        }

        private async void AddCardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Ra lệnh cho ViewModel tải Decks
            await _viewModel.LoadDecksAsync();
        }
    }
}