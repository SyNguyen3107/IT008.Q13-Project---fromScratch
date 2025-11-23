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
            try
            {
                var newCard = new Card
                {
                    DeckId = SelectedDeck.ID,
                    FrontText = this.FrontText,
                    Answer = this.Answer,
                    BackText = this.BackText ?? "",
                    FrontImagePath = this.FrontImagePath,
                    FrontAudioPath = this.FrontAudioPath,
                    BackImagePath = this.BackImagePath,
                    BackAudioPath = this.BackAudioPath,
                };

                await _cardRepository.AddAsync(newCard);
                _messenger.Send(new CardAddedMessage(SelectedDeck.ID));

                // Reset Form
                FrontText = string.Empty; Answer = string.Empty; BackText = string.Empty;
                RemoveFrontImage(); RemoveFrontAudio();
                RemoveBackImage(); RemoveBackAudio();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

            [RelayCommand]
        private void Cancel(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

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

        // --- MEDIA COMMANDS (HỖ TRỢ ONLINE LINK) ---
        // Hàm xử lý chung cho việc chọn file
        private void SetMedia(string path, ref string pathProperty, ref string nameProperty)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            pathProperty = path;

            // Nếu là link online (http/https)
            if (path.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
            {
                nameProperty = "🌐 Online Link";
            }
            else // Nếu là file local
            {
                nameProperty = Path.GetFileName(path);
            }
        }
        // Chọn Media
        [RelayCommand]
        private void PickFrontImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null)
            {
                FrontImagePath = path;
                SetMedia(path, ref _frontImagePath, ref _frontImageName);
                // Cập nhật lại property để UI nhận biết thay đổi
                OnPropertyChanged(nameof(FrontImagePath));
                OnPropertyChanged(nameof(FrontImageName));
            }
        }
        [RelayCommand]
        private void PickFrontAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null)
            {
                FrontAudioPath = path;
                SetMedia(path, ref _frontAudioPath, ref _frontAudioName);
                OnPropertyChanged(nameof(FrontAudioPath));
                OnPropertyChanged(nameof(FrontAudioName));
            }
        }
        [RelayCommand]
        private void PickBackImage()
        {
            var path = PickFile("Images|*.png;*.jpg;*.jpeg;*.gif|All Files|*.*");
            if (path != null)
            {
                BackImagePath = path;
                SetMedia(path, ref _backImagePath, ref _backImageName);
                OnPropertyChanged(nameof(BackImagePath));
                OnPropertyChanged(nameof(BackImageName));
            }
        }
        [RelayCommand]
        private void PickBackAudio()
        {
            var path = PickFile("Audio|*.mp3;*.wav;*.m4a|All Files|*.*");
            if (path != null)
            {
                BackAudioPath = path;
                SetMedia(path, ref _backAudioPath, ref _backAudioName);
                OnPropertyChanged(nameof(BackAudioPath));
                OnPropertyChanged(nameof(BackAudioName));
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

        // --- LỆNH DÁN LINK (PASTE URL) ---
        // Tham số "type" để biết đang dán vào đâu: "FrontImage", "FrontAudio", v.v.
        [RelayCommand]
        private void PasteLink(string type)
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (text.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                {
                    switch (type)
                    {
                        case "FrontImage": SetMedia(text, ref _frontImagePath, ref _frontImageName); OnPropertyChanged(nameof(FrontImagePath)); OnPropertyChanged(nameof(FrontImageName)); break;
                        case "FrontAudio": SetMedia(text, ref _frontAudioPath, ref _frontAudioName); OnPropertyChanged(nameof(FrontAudioPath)); OnPropertyChanged(nameof(FrontAudioName)); break;
                        case "BackImage": SetMedia(text, ref _backImagePath, ref _backImageName); OnPropertyChanged(nameof(BackImagePath)); OnPropertyChanged(nameof(BackImageName)); break;
                        case "BackAudio": SetMedia(text, ref _backAudioPath, ref _backAudioName); OnPropertyChanged(nameof(BackAudioPath)); OnPropertyChanged(nameof(BackAudioName)); break;
                    }
                }
                else MessageBox.Show("Invalid URL.");
            }
        }

        // --- LỆNH MỞ FILE (LOCAL HOẶC ONLINE)---
        [RelayCommand]
        private void OpenFileLocation(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                // Trường hợp 1: Link Online (http/https) -> Mở trình duyệt
                if (filePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                // Trường hợp 2: File Local -> Mở File Explorer và chọn file
                else if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select, \"{filePath}\"");
                }
                else
                {
                    MessageBox.Show("File does not exist or directory is not valid.", "Notice");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Cannot open the following file: {ex.Message}", "Error");
            }
        }
        // --- NEW COMMANDS: XÓA MEDIA ---

        [RelayCommand]
        private void RemoveFrontImage()
        {
            FrontImagePath = null;
            FrontImageName = null;
        }

        [RelayCommand]
        private void RemoveFrontAudio()
        {
            FrontAudioPath = null;
            FrontAudioName = null;
        }

        [RelayCommand]
        private void RemoveBackImage()
        {
            BackImagePath = null;
            BackImageName = null;
        }

        [RelayCommand]
        private void RemoveBackAudio()
        {
            BackAudioPath = null;
            BackAudioName = null;
        }
    }
}