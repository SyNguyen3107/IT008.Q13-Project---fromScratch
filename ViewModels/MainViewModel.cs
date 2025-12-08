using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using EasyFlips.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace EasyFlips.ViewModels
{
    // --- SỬA LỖI: Thêm IRecipient<DeckUpdatedMessage> ---
    public partial class MainViewModel : ObservableObject,
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
        private readonly IAuthService _authService;

        // Nếu bạn muốn hiển thị tên user đang đăng nhập trên Main Window
        [ObservableProperty]
        private string currentEmail;
        [ObservableProperty]
        private bool _isConnected;

        public ObservableCollection<Deck> Decks { get; } = new ObservableCollection<Deck>();

        public MainViewModel(IDeckRepository deckRepository,
                                 INavigationService navigationService,
                                 IMessenger messenger,
                                 ImportService importService,
                                 ExportService exportService,
                                 UserSession userSession,
                                 IAuthService authService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;
            _messenger = messenger;
            _exportService = exportService;
            _importService = importService;

            _authService = authService;
            _messenger.RegisterAll(this);
            // Lấy thông tin hiển thị (nếu cần)
            CurrentEmail = userSession.Email;
            // Khởi tạo trạng thái kết nối mạng
            IsConnected = NetworkService.Instance.IsConnected;
            // Đăng ký lắng nghe thay đổi trạng thái mạng
            NetworkService.Instance.ConnectivityChanged += (status) =>
            {
                IsConnected = status;
            };

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
                    // Thay vì gán đè (Decks[index] = ..), ta xóa đi và chèn lại.
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
            MessageBox.Show($"In development", "Options");
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

                        MessageBox.Show($"'{newDeck.Name}' successfully Imported!", "Imported");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error occurred: {ex.Message}", "Errors");
                }
            }
        }

        [RelayCommand]
        private async Task Sync()
        {
            _navigationService.ShowSyncWindow();
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
                MessageBox.Show("Successfully Exported!", "Notice");
            }
        }

        [RelayCommand]
        private async Task DeleteDeck(Deck deck)
        {
            // 1. Kiểm tra null
            if (deck == null) return;

            // 2. Hiển thị hộp thoại xác nhận (Confirmation Dialog)
            var result = MessageBox.Show(
                $"Are you sure you want to permanently delete '{deck.Name}' ?\n\nWARNING: All {deck.NewCount + deck.LearnCount + deck.DueCount} card(s) inside will be permanently deleted too!",
                "Confirm deletion",
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

                MessageBox.Show("Successfully deleted deck!", "Notice");
            }
        }
        [RelayCommand]
        private void Logout()
        {
            // 1. Xử lý logic đăng xuất (Xóa Session, Xóa Remember Me...)
            _authService.Logout();

            // 2. Mở màn hình Login
            _navigationService.ShowLoginWindow();

            // 3. Đóng MainWindow hiện tại
            CloseCurrentWindow();
        }
        [RelayCommand]
        private void CloseCurrentWindow()
        {
            // Tìm cửa sổ đang giữ ViewModel này (chính là MainWindow) và đóng nó
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
    }
}