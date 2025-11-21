using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Models;
using Microsoft.Win32; // Dùng cho OpenFileDialog
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows; // Dùng cho MessageBox
using IT008.Q13_Project___fromScratch.Views; // Cần cái này để gọi ChooseDeckWindow

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class AddCardViewModel : ObservableObject
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository; // <-- Cần thêm repo này

        // --- Các thuộc tính bind với Giao diện (View) ---

        // Danh sách Deck để chọn từ ComboBox
        public ObservableCollection<Deck> AllDecks { get; } = new ObservableCollection<Deck>();

        // Deck đang được chọn trong ComboBox
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))] // Cập nhật trạng thái nút Save
        private Deck _selectedDeck;

        // Nội dung thẻ
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))] // Cập nhật trạng thái nút Save
        private string _frontText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))] // Cập nhật trạng thái nút Save
        private string _backText;

        // Đường dẫn file (optional)
        [ObservableProperty]
        private string _frontImagePath;

        [ObservableProperty]
        private string _frontAudioPath;

        [ObservableProperty]
        private string _backImagePath;

        [ObservableProperty]
        private string _backAudioPath;


        // Constructor
        public AddCardViewModel(ICardRepository cardRepository, IDeckRepository deckRepository)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository; // <-- Tiêm DeckRepository
        }

        // --- Hàm tải dữ liệu ---

        // Hàm này phải được gọi từ code-behind (AddCardWindow.xaml.cs)
        public async Task LoadDecksAsync()
        {
            AllDecks.Clear();
            var decks = await _deckRepository.GetAllAsync(); // Lấy tất cả deck từ CSDL
            foreach (var deck in decks)
            {
                AllDecks.Add(deck);
            }
        }

        // --- Các Command (Nút bấm) ---

        // Điều kiện để nút "Save" có thể được bấm
        private bool CanSaveCard()
        {
            return !string.IsNullOrWhiteSpace(FrontText) &&
                   !string.IsNullOrWhiteSpace(BackText) &&
                   SelectedDeck != null; // Phải chọn Deck
        }

        [RelayCommand(CanExecute = nameof(CanSaveCard))]
        private async Task SaveCard()
        {
            // 1. Tạo đối tượng Card mới
            var newCard = new Card
            {
                DeckId = SelectedDeck.ID, // Quan trọng!
                FrontText = this.FrontText,
                BackText = this.BackText,
                FrontImagePath = this.FrontImagePath,
                FrontAudioPath = this.FrontAudioPath,
                BackImagePath = this.BackImagePath,
                BackAudioPath = this.BackAudioPath,

                // CardRepository sẽ tự động tạo CardProgress mặc định
            };

            // 2. Lưu vào CSDL
            await _cardRepository.AddAsync(newCard);

            // 3. Xóa các trường để chuẩn bị cho thẻ tiếp theo
            FrontText = "";
            BackText = "";
            FrontImagePath = null;
            FrontAudioPath = null;
            BackImagePath = null;
            BackAudioPath = null;

            // (Không cần đóng cửa sổ, để người dùng có thể thêm nhiều thẻ)
        }

        // Command mẫu cho việc chọn file
        // Command Đóng cửa sổ
        [RelayCommand]
        private void Cancel(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        // Command mở cửa sổ chọn deck
        [RelayCommand]
        private async Task ChooseDeck()
        {
            var chooseDeckWindow = new ChooseDeckWindow();
            chooseDeckWindow.ShowDialog();
            // Hiển thị tên deck đã chọn
            if (chooseDeckWindow.SelectedDeck != null)
                SelectedDeck = chooseDeckWindow.SelectedDeck;
        }

        // Command thêm Media vào Front
        [RelayCommand]
        private void PickFrontMedia()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                // Filter = "Image files (*.png;*.jpg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
                Title = "Select media for FRONT",
                Filter =
                    "Media files (*.png;*.jpg;*.jpeg;*.gif;*.mp3;*.wav;*.mp4;*.avi)|*.png;*.jpg;*.jpeg;*.gif;*.mp3;*.wav;*.mp4;*.avi|" +
                    "Images (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|" +
                    "Audio (*.mp3;*.wav)|*.mp3;*.wav|" +
                    "Video (*.mp4;*.avi)|*.mp4;*.avi|" +
                    "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                FrontImagePath = dialog.FileName; // Cập nhật đường dẫn
            }
        }

        // Command thêm Media vào Back
        [RelayCommand]
        private void PickBackMedia()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select media for BACK",
                Filter =
                    "Media files (*.png;*.jpg;*.jpeg;*.gif;*.mp3;*.wav;*.mp4;*.avi)|*.png;*.jpg;*.jpeg;*.gif;*.mp3;*.wav;*.mp4;*.avi|" +
                    "Images (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|" +
                    "Audio (*.mp3;*.wav)|*.mp3;*.wav|" +
                    "Video (*.mp4;*.avi)|*.mp4;*.avi|" +
                    "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                BackImagePath = dialog.FileName; 
            }
        }
        // [RelayCommand] private void PickFrontAudio() { ... }
        // [RelayCommand] private void PickBackAudio() { ... }

        // ChooseDeck Command
    }
}