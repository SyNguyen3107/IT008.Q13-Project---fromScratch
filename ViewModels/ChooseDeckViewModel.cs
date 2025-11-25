using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces; // <-- Cần thêm để dùng IDeckRepository
using EasyFlips.Models;
using System.Collections.ObjectModel;
using System.Windows;
namespace EasyFlips.ViewModels
{
    public partial class ChooseDeckViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;
        // Danh sách các deck để hiển thị
        [ObservableProperty]
        private ObservableCollection<Deck> decks = new();

        // Deck được chọn
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChooseCommand))] // Cập nhật trạng thái nút Choose
        private Deck? _selectedDeck;

        // Constructor: Nhận Repository từ DI
        public ChooseDeckViewModel(IDeckRepository deckRepository, INavigationService navigationService)
        {
            _deckRepository = deckRepository;

            // Gọi hàm tải dữ liệu. Dùng _ = để bỏ qua cảnh báo async trong constructor
            _ = LoadDecksAsync();
            _navigationService = navigationService;
        }

        // Command Choose 
        [RelayCommand]
        private void Choose(Window window)
        {
            if (SelectedDeck == null)
            {
                MessageBox.Show("Please choose a deck!", "No deck selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Gửi deck được chọn ra ngoài window (qua DialogResult)
            window.Tag = SelectedDeck;
            window.DialogResult = true;
            window.Close();
        }

        // Commmand Cancel
        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }

        // Command Add
        [RelayCommand]
        private void Add()
        {
            _navigationService.ShowCreateDeckWindow();
            LoadDecksAsync();
        }
        // Nạp danh sách Deck
        public void LoadDecks(IEnumerable<Deck> deckList)
        {
            Decks.Clear();
            foreach (var deck in deckList)
                Decks.Add(deck);
        }
        public async Task LoadDecksAsync()
        {
            // Lấy dữ liệu thật từ Database
            var deckList = await _deckRepository.GetAllAsync();

            Decks.Clear();
            foreach (var deck in deckList)
            {
                Decks.Add(deck);
            }
        }
    }
}
