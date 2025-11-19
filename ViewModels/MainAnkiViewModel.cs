using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq; // <-- Cần thêm để dùng .FirstOrDefault()
using CommunityToolkit.Mvvm.Messaging;
using IT008.Q13_Project___fromScratch.Messages;
using System.Windows;
using Microsoft.Win32;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    // --- SỬA LỖI: Thêm IRecipient<DeckUpdatedMessage> ---
    public partial class MainAnkiViewModel : ObservableObject,
                                             IRecipient<DeckAddedMessage>,
                                             IRecipient<DeckUpdatedMessage>
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;
        private readonly IMessenger _messenger;
        private readonly ExportService _exportService;

        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>();

        public MainAnkiViewModel(IDeckRepository deckRepository,
                                 INavigationService navigationService,
                                 IMessenger messenger,
                                 ExportService exportService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;
            _messenger = messenger;
            _exportService = exportService;

            _messenger.RegisterAll(this);
        }

        // Xử lý khi có Deck mới (Add)
        public void Receive(DeckAddedMessage message)
        {
            var newDeck = message.Value;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Decks.Add(newDeck);
            });
        }

        // --- HÀM MỚI: Xử lý khi Deck bị sửa đổi (Rename) ---
        public void Receive(DeckUpdatedMessage message)
        {
            var updatedDeck = message.Value;

            // Đảm bảo code chạy trên luồng giao diện (UI Thread)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. Tìm vị trí của Deck này trong danh sách hiện tại bằng ID
                var existingDeck = Decks.FirstOrDefault(d => d.ID == updatedDeck.ID);

                if (existingDeck != null)
                {
                    int index = Decks.IndexOf(existingDeck);

                    // 2. ÉP GIAO DIỆN CẬP NHẬT (Force UI Update)
                    // Thay vì gán đè (Decks[index] = ...), ta xóa đi và chèn lại.
                    // Điều này gửi tín hiệu "CollectionChanged" mạnh mẽ tới ListView,
                    // buộc nó phải vẽ lại dòng này với tên mới ngay lập tức.

                    Decks.RemoveAt(index);            // Xóa deck cũ (tên cũ)
                    Decks.Insert(index, updatedDeck); // Chèn deck mới (tên mới) vào đúng vị trí đó
                }
            });


        }

        public async Task LoadDecksAsync()
        {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear();
            foreach (var deck in decks)
            {
                Decks.Add(deck);
            }
        }

        // --- CÁC COMMAND (GIỮ NGUYÊN) ---

        [RelayCommand]
        private void ShowDeckChosen(Deck selectedDeck)
        {
            if (selectedDeck != null)
                _navigationService.ShowDeckChosenWindow(selectedDeck.ID);
        }

        [RelayCommand]
        private void StartStudy(Deck selectedDeck)
        {
            if (selectedDeck == null) return;
            _navigationService.ShowStudyWindow(selectedDeck.ID);
        }

        [RelayCommand]
        private void CreateDeck()
        {
            _navigationService.ShowCreateDeckWindow();
        }

        [RelayCommand]
        private void ImportFile()
        {
            _navigationService.ImportFileWindow();
        }

        [RelayCommand]
        private void AddCard()
        {
            _navigationService.ShowAddCardWindow();
        }

        [RelayCommand]
        private void RenameDeck(Deck deck)
        {
            if (deck == null) return;
            _navigationService.ShowDeckRenameWindow(deck);
        }

        [RelayCommand]
        private void DeckOptions(Deck deck)
        {
            if (deck == null) return;
            MessageBox.Show($"Cài đặt cho deck '{deck.Name}'", "Options");
        }

        [RelayCommand]
        private async Task ExportDeck(Deck deck)
        {
            if (deck == null) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = deck.Name;
            dlg.DefaultExt = ".json";
            dlg.Filter = "Anki JSON Files (.json)|*.json";

            if (dlg.ShowDialog() == true)
            {
                await _exportService.ExportDeckToJsonAsync(deck.ID, dlg.FileName);
                MessageBox.Show("Export thành công!", "Thông báo");
            }
        }

        [RelayCommand]
        private async Task DeleteDeck(Deck deck)
        {
            if (deck == null) return;

            var result = MessageBox.Show($"Bạn có chắc muốn xóa bộ thẻ '{deck.Name}'?\nTất cả thẻ bên trong sẽ bị xóa vĩnh viễn.",
                                         "Xác nhận xóa",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _deckRepository.DeleteAsync(deck.ID);
                Decks.Remove(deck);
            }
        }
    }
}