using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using Microsoft.Extensions.DependencyInjection; // <-- CẦN THÊM: Để dùng GetRequiredService
using Microsoft.Win32; // Dùng cho OpenFileDialog
using System.Collections.ObjectModel;
using System.Diagnostics; // Cần thêm để dùng Process.Start
using System.IO; // Cần thêm để dùng Path.GetFileName
using System.Windows; // Dùng cho MessageBox
using EasyFlips.Helpers; //Để dùng PathHelper

namespace EasyFlips.ViewModels
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
                    DeckId = SelectedDeck.Id,
                    FrontText = this.FrontText,
                    Answer = this.Answer,
                    BackText = this.BackText ?? "",

                    // Lưu ý: Các biến này bây giờ chỉ chứa Tên File (nếu là local) hoặc URL (nếu online)
                    // Nhờ logic trong SetMedia xử lý trước đó.
                    FrontImagePath = this.FrontImagePath,
                    FrontAudioPath = this.FrontAudioPath,
                    BackImagePath = this.BackImagePath,
                    BackAudioPath = this.BackAudioPath,
                };

                await _cardRepository.AddAsync(newCard);
                _messenger.Send(new CardAddedMessage(SelectedDeck.Id));

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

        // LOGIC XỬ LÝ MEDIA

        //Hàm xử lý chung: Copy file vào AppData và lấy tên file tương đối
        //sourcePath: Đường dẫn gốc người dùng chọn
        //pathProperty: Biến tham chiếu để lưu Path (sẽ lưu vào DB)
        //nameProperty: Biến tham chiếu để lưu Tên hiển thị
        private void ProcessAndSetMedia(string sourcePath, ref string pathProperty, ref string nameProperty)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return;

            try
            {
                // TRƯỜNG HỢP 1: Link Online
                if (sourcePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    pathProperty = sourcePath; // Lưu nguyên link
                    nameProperty = "🌐 Online Link";
                }
                // TRƯỜNG HỢP 2: File Local (Cần copy)
                else
                {
                    // 1. Lấy tên file gốc (vd: cat.png)
                    string fileName = Path.GetFileName(sourcePath);

                    // 2. Tạo tên file duy nhất (vd: cat_8s7d6f5g.png) để tránh trùng
                    string extension = Path.GetExtension(fileName);
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string uniqueName = $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}";

                    // 3. Tạo đường dẫn đích trong AppData/Roaming/EasyFlips/Media
                    string mediaFolder = PathHelper.GetMediaFolderPath();
                    string destPath = Path.Combine(mediaFolder, uniqueName);

                    // 4. Copy file vào đó
                    File.Copy(sourcePath, destPath, overwrite: true);

                    // 5. QUAN TRỌNG: Chỉ lưu TÊN FILE DUY NHẤT vào biến (để lưu DB)
                    pathProperty = uniqueName;

                    // 6. Hiển thị tên gốc cho người dùng dễ nhìn
                    nameProperty = fileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing media: {ex.Message}", "Error");
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
                ProcessAndSetMedia(path, ref _frontImagePath, ref _frontImageName);
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
                ProcessAndSetMedia(path, ref _frontAudioPath, ref _frontAudioName);
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
                ProcessAndSetMedia(path, ref _backImagePath, ref _backImageName);
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
                ProcessAndSetMedia(path, ref _backAudioPath, ref _backAudioName);
                OnPropertyChanged(nameof(BackAudioPath));
                OnPropertyChanged(nameof(BackAudioName));
            }
        }

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
                if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Tái sử dụng hàm ProcessAndSetMedia (nó sẽ nhận diện là http và không copy)
                    switch (type)
                    {
                        case "FrontImage": ProcessAndSetMedia(text, ref _frontImagePath, ref _frontImageName); OnPropertyChanged(nameof(FrontImagePath)); OnPropertyChanged(nameof(FrontImageName)); break;
                        case "FrontAudio": ProcessAndSetMedia(text, ref _frontAudioPath, ref _frontAudioName); OnPropertyChanged(nameof(FrontAudioPath)); OnPropertyChanged(nameof(FrontAudioName)); break;
                        case "BackImage": ProcessAndSetMedia(text, ref _backImagePath, ref _backImageName); OnPropertyChanged(nameof(BackImagePath)); OnPropertyChanged(nameof(BackImageName)); break;
                        case "BackAudio": ProcessAndSetMedia(text, ref _backAudioPath, ref _backAudioName); OnPropertyChanged(nameof(BackAudioPath)); OnPropertyChanged(nameof(BackAudioName)); break;
                    }
                }
                else MessageBox.Show("Invalid URL. Please copy a link starting with http/https.");
            }
        }

        // --- LỆNH MỞ FILE (ĐÃ CẬP NHẬT LOGIC RELATIVE PATH) ---
        [RelayCommand]
        private void OpenFileLocation(string relativeOrUrlPath)
        {
            if (string.IsNullOrEmpty(relativeOrUrlPath)) return;

            try
            {
                // TH1: Link Online
                if (relativeOrUrlPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo { FileName = relativeOrUrlPath, UseShellExecute = true });
                }
                // TH2: File Local (Chỉ có tên file) -> Cần ghép đường dẫn full
                else
                {
                    // Dùng PathHelper để lấy đường dẫn thực tế trên máy này
                    string fullPath = PathHelper.GetFullPath(relativeOrUrlPath);

                    if (File.Exists(fullPath))
                    {
                        Process.Start("explorer.exe", $"/select, \"{fullPath}\"");
                    }
                    else
                    {
                        MessageBox.Show($"File not found at:\n{fullPath}", "Notice");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open file: {ex.Message}", "Error");
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