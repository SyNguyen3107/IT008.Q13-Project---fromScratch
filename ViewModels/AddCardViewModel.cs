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
using System.IO; // Cần thêm để dùng Path.GetFileName
using System.Diagnostics; // Cần thêm để dùng Process.Start

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class AddCardViewModel : ObservableObject
    {
        private readonly ICardRepository _cardRepository;
        private readonly IDeckRepository _deckRepository;

        private readonly IMessenger _messenger;//Dùng để gửi tin nhắn cập nhật danh sách thẻ

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

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCardCommand))]
        private string _answer = string.Empty;

        // Đường dẫn file
        [ObservableProperty]
        private string _frontImagePath;

        [ObservableProperty]
        private string _frontAudioPath;

        [ObservableProperty]
        private string _backImagePath;

        [ObservableProperty]
        private string _backAudioPath;

        //Tên file ảnh hiển thị
        [ObservableProperty]
        private string _frontImageName;

        [ObservableProperty]
        private string _backImageName;

        //Tên file âm thanh hiển thị trên nút 
        [ObservableProperty]
        private string _frontAudioName;

        [ObservableProperty]
        private string _backAudioName;
        // Constructor
        public AddCardViewModel(ICardRepository cardRepository, IDeckRepository deckRepository,
                                IMessenger messenger)
        {
            _cardRepository = cardRepository;
            _deckRepository = deckRepository;
            _messenger = messenger;
        }

        // Hàm tải dữ liệu
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
                   !string.IsNullOrWhiteSpace(Answer) &&
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
                Answer = this.Answer,
                FrontImagePath = this.FrontImagePath,
                FrontAudioPath = this.FrontAudioPath,
                BackImagePath = this.BackImagePath,
                BackAudioPath = this.BackAudioPath,
                // CardRepository sẽ tự động tạo CardProgress mặc định
            };

            await _cardRepository.AddAsync(newCard);
            _messenger.Send(new CardAddedMessage(SelectedDeck.ID));
            // Reset Form (Giữ lại Deck để nhập tiếp)
            FrontText = "";
            BackText = "";
            Answer = string.Empty;
            FrontImagePath = null;
            FrontAudioPath = null;
            BackImagePath = null;
            BackAudioPath = null;
            FrontAudioName = null;
            BackAudioName = null;
            FrontImageName = null;
            BackImageName = null;
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
            var chooseDeckWindow = App.ServiceProvider.GetRequiredService<ChooseDeckWindow>();

            chooseDeckWindow.ShowDialog();

            // Hiển thị tên deck đã chọn
            if (chooseDeckWindow.SelectedDeck != null)
            {
                SelectedDeck = chooseDeckWindow.SelectedDeck;
            }
        }

        // Command thêm Media vào Front
        //[RelayCommand]
        //private void PickFrontMedia()
        //{
        //    OpenFileDialog dialog = new OpenFileDialog
        //    {
        //        Title = "Select media for FRONT",
        //        Filter = "All files (*.*)|*.*"
        //    };

        //    if (dialog.ShowDialog() == true)
        //    {
        //        FrontImagePath = dialog.FileName;
        //    }
        //}

        //// Command thêm Media vào Back
        //[RelayCommand]
        //private void PickBackMedia()
        //{
        //    OpenFileDialog dialog = new OpenFileDialog
        //    {
        //        Title = "Select media for BACK",
        //        Filter = "All files (*.*)|*.*"
        //    };

        //    if (dialog.ShowDialog() == true)
        //    {
        //        BackImagePath = dialog.FileName;
        //    }
        //}

        // Chọn Media
        [RelayCommand]
        private void PickFrontImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null)
            {
                FrontImagePath = path;
                FrontImageName = Path.GetFileName(path);
            }
        }

        [RelayCommand]
        private void PickFrontAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null)
            {
                FrontAudioPath = path;
                FrontAudioName = Path.GetFileName(path);
            }
        }

        [RelayCommand]
        private void PickBackImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null)
            {
                BackImagePath = path;
                BackImageName = Path.GetFileName(path);
            }
        }

        [RelayCommand]
        private void PickBackAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null)
            {
                BackAudioPath = path;
                BackAudioName = Path.GetFileName(path);
            }
        }

        // Hàm phụ trợ có tham số filter
        private string? PickFile(string filter)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select Media",
                Filter = filter
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        // --- LỆNH MỞ FILE EXPLORER ---
        [RelayCommand]
        private void OpenFileLocation(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                // Mở Explorer và highlight file đó (/select)
                Process.Start("explorer.exe", $"/select, \"{filePath}\"");
            }
            else
            {
                MessageBox.Show("File không tồn tại hoặc đường dẫn bị lỗi.", "Lỗi");
            }
        }
        // Các command phụ khác...
    }
}