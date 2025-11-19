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
                _viewModel.ShowDeckChosenCommandCommand.Execute(selectedDeck);
            }

        }
    }
}

