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
                                             IRecipient<DeckUpdatedMessage>,
                                             IRecipient<CardAddedMessage>,
                                             IRecipient<StudySessionCompletedMessage>
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;
        private readonly IMessenger _messenger;
        private readonly ImportService _importService;
        private readonly ExportService _exportService;

        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>();

        public MainAnkiViewModel(IDeckRepository deckRepository,
                                 INavigationService navigationService,
                                 IMessenger messenger,
                                 ImportService importService,
                                 ExportService exportService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;
            _messenger = messenger;
            _exportService = exportService;
            _importService = importService;

            _messenger.RegisterAll(this);
        }

        // Xử lý khi có Deck mới (Add hoặc Import)
        public void Receive(DeckAddedMessage message)
        {
            var newDeck = message.Value;
            Application.Current.Dispatcher.Invoke(async () =>
            {
                // Reload lại deck từ database để có thống kê chính xác
                await LoadDecksAsync();
            });
        }

        // Xử lý khi thêm thẻ mới vào Deck (Card Added)
        public void Receive(CardAddedMessage message)
        {
            int deckId = message.Value;

            Application.Current.Dispatcher.Invoke(async () =>
            {
                // Reload lại toàn bộ decks để cập nhật số lượng thẻ New chính xác
                await LoadDecksAsync();
            });
        }

        // Xử lý khi Deck bị sửa đổi (Rename)
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

        // Xử lý khi học xong
        public void Receive(StudySessionCompletedMessage message)
        {
            int deckId = message.Value;

            // Khi học xong, số liệu New/Learn/Due chắc chắn thay đổi.
            // Reload lại toàn bộ danh sách Deck từ DB để cập nhật số liệu chính xác
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await LoadDecksAsync();
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
        private async Task ImportFile()
        {
            // 1. Mở hộp thoại chọn file
            OpenFileDialog dlg = new OpenFileDialog();
            // Thiết lập bộ lọc để chỉ hiển thị các file .zip.json
            dlg.Filter = "Deck Files (.zip)|*.zip";
            dlg.Title = "Import Deck";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 2. Gọi Service để Import ( đọc file zip)
                    var newDeck = await _importService.ImportDeckFromZipAsync(dlg.FileName);

                    if (newDeck != null)
                    {
                        // 3. Gửi tin nhắn để cập nhật UI (hoặc thêm trực tiếp vào Decks)
                        // Vì chúng ta đang ở MainViewModel, thêm trực tiếp vào Decks cũng được
                        // Nhưng dùng Messenger cho nhất quán
                        _messenger.Send(new DeckAddedMessage(newDeck));

                        MessageBox.Show($"Đã nhập bộ thẻ '{newDeck.Name}' thành công!", "Thành công");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi nhập file: {ex.Message}", "Lỗi");
                }
            }
        }

        [RelayCommand]
        private async Task ExportDeck(Deck deck)
        {
            if (deck == null) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = deck.Name;               // gợi ý tên file: TênDeck (không thêm .json)
            dlg.DefaultExt = ".zip";                  // mặc định là .zip
            dlg.Filter = "Deck Package (.zip)|*.zip"; // chỉ cho lưu file .zip
            dlg.Title = "Export Deck";


            if (dlg.ShowDialog() == true)
            {
                // Nếu người dùng nhập "abc.json" → thành "abc.json.zip"
                string zipPath = dlg.FileName.EndsWith(".zip")
                    ? dlg.FileName
                    : dlg.FileName + ".zip";

                await _exportService.ExportDeckToZipAsync(deck.ID, zipPath);
                MessageBox.Show("Export thành công!", "Thông báo");
            }
        }

        [RelayCommand]
        private async Task DeleteDeck(Deck deck)
        {
            // 1. Kiểm tra null
            if (deck == null) return;

            // 2. Hiển thị hộp thoại xác nhận (Confirmation Dialog)
            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa bộ thẻ '{deck.Name}' không?\n\nCẢNH BÁO: Tất cả {deck.NewCount + deck.LearnCount + deck.DueCount} thẻ bên trong sẽ bị xóa vĩnh viễn!",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // 3. Nếu người dùng chọn Yes
            if (result == MessageBoxResult.Yes)
            {
                // 3a. Xóa trong Database
                await _deckRepository.DeleteAsync(deck.ID);

                // 3b. Xóa trên Giao diện (UI)
                // Chúng ta tìm đối tượng trong list hiện tại bằng ID để đảm bảo xóa đúng cái đang hiển thị
                var deckToRemove = Decks.FirstOrDefault(d => d.ID == deck.ID);
                if (deckToRemove != null)
                {
                    Decks.Remove(deckToRemove);
                }

                MessageBox.Show("Đã xóa thành công!", "Thông báo");
            }
        }
    }
}