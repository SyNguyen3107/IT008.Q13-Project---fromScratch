using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Views; // Cần cái này để gọi ChooseDeckWindow
using Microsoft.Extensions.DependencyInjection; // <-- CẦN THÊM: Để dùng GetRequiredService
using Microsoft.Win32; // Dùng cho OpenFileDialog
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows; // Dùng cho MessageBox
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using IT008.Q13_Project___fromScratch.Messages;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class AddCardViewModel : ObservableObject
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository;

        private readonly IMessenger _messenger;

        // --- Các thuộc tính bind với Giao diện (View) ---

        // Danh sách Deck để chọn từ ComboBox (Có thể không dùng nữa nếu dùng ChooseDeckWindow)
        public ObservableCollection<Deck> AllDecks { get; } = new ObservableCollection<Deck>();

        // Deck đang được chọn
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private Deck _selectedDeck;

        // Nội dung thẻ
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _frontText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _backText;

        // Đường dẫn file
        [ObservableProperty]
        private string _frontImagePath;

        [ObservableProperty]
        private string _frontAudioPath;

        [ObservableProperty]
        private string _backImagePath;

        [ObservableProperty]
        private string _backAudioPath;


        // Constructor
        public AddCardViewModel(ICardRepository cardRepository, IDeckRepository deckRepository,
                                IMessenger messenger)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository;
            _messenger = messenger;
        }

        // Hàm tải dữ liệu (Giữ nguyên nếu bạn vẫn dùng ComboBox ở đâu đó)
        public async Task LoadDecksAsync()
        {
            AllDecks.Clear();
            var decks = await _deckRepository.GetAllAsync();
            foreach (var deck in decks)
            {
                AllDecks.Add(deck);
            }
        }

        // --- Các Command (Nút bấm) ---

        private bool CanSaveCard()
        {
            return !string.IsNullOrWhiteSpace(FrontText) &&
                   !string.IsNullOrWhiteSpace(BackText) &&
                   SelectedDeck != null;
        }

        [RelayCommand(CanExecute = nameof(CanSaveCard))]
        private async Task SaveCard()
        {
            var newCard = new Card
            {
                DeckId = SelectedDeck.ID,
                FrontText = this.FrontText,
                BackText = this.BackText,
                FrontImagePath = this.FrontImagePath,
                FrontAudioPath = this.FrontAudioPath,
                BackImagePath = this.BackImagePath,
                BackAudioPath = this.BackAudioPath,
                // CardRepository sẽ tự động tạo CardProgress mặc định
            };

            await _cardRepository.AddAsync(newCard);
            _messenger.Send(new CardAddedMessage(SelectedDeck.ID));
            // Reset Form
            FrontText = "";
            BackText = "";
            FrontImagePath = null;
            FrontAudioPath = null;
            BackImagePath = null;
            BackAudioPath = null;
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        // --- SỬA LỖI Ở ĐÂY ---
        [RelayCommand]
        private async Task ChooseDeck()
        {
            // Thay vì: var chooseDeckWindow = new ChooseDeckWindow(); (LỖI)
            // Hãy dùng:
            var chooseDeckWindow = App.ServiceProvider.GetRequiredService<ChooseDeckWindow>();

            chooseDeckWindow.ShowDialog();

            // Hiển thị tên deck đã chọn
            if (chooseDeckWindow.SelectedDeck != null)
            {
                SelectedDeck = chooseDeckWindow.SelectedDeck;
            }
        }

        // Command thêm Media vào Front
        [RelayCommand]
        private void PickFrontMedia()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select media for FRONT",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                FrontImagePath = dialog.FileName;
            }
        }

        // Command thêm Media vào Back
        [RelayCommand]
        private void PickBackMedia()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select media for BACK",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                BackImagePath = dialog.FileName;
            }
        }

        // Các command phụ khác...
    }
}