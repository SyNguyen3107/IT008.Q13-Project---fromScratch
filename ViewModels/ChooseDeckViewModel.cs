using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using IT008.Q13_Project___fromScratch.Interfaces; // <-- Cần thêm để dùng IDeckRepository
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class ChooseDeckViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        // Danh sách các deck để hiển thị
        [ObservableProperty]
        private ObservableCollection<Deck> decks = new();

        // Deck được chọn
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChooseCommand))] // Cập nhật trạng thái nút Choose
        private Deck? _selectedDeck;

        // Constructor: Nhận Repository từ DI
        public ChooseDeckViewModel(IDeckRepository deckRepository)
        {
            _deckRepository = deckRepository;

            // Gọi hàm tải dữ liệu. Dùng _ = để bỏ qua cảnh báo async trong constructor
            _ = LoadDecksAsync();
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
            var win = new CreateDeckWindow();
            win.ShowDialog();
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
