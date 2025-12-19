using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using EasyFlips.ViewModels;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace EasyFlips.Services
{
    public class NavigationService : INavigationService
    {
        //DI chính sẽ được tiêm vào đây
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;

        // Constructor: Nhận DI
        public NavigationService(IServiceProvider serviceProvider, IMessenger messenger)
        {
            _serviceProvider = serviceProvider;
            _messenger = messenger;
        }

        public void ShowAddCardWindow()
        {
            var window = _serviceProvider.GetRequiredService<AddCardWindow>();
            window.ShowDialog();
        }

        public void ShowCardWindow()
        {
            //Thêm lệnh mở cửa sổ ShowCardWindow
        }
        public void ShowCreateDeckWindow()
        {
            // Yêu cầu "thợ điện" tạo một cửa sổ mới
            // DI sẽ tự động tìm CreateDeckWindow và CreateDeckViewModel
            var window = _serviceProvider.GetRequiredService<CreateDeckWindow>();

            // ShowDialog() để nó chặn cửa sổ chính, bắt người dùng phải tương tác
            window.ShowDialog();
        }
        public void ShowDeckRenameWindow(Deck deck)
        {
            // 1. Lấy Window từ DI (Dependency Injection)
            var window = _serviceProvider.GetRequiredService<DeckRenameWindow>();

            // 2. Lấy ViewModel từ DataContext (đã được gán trong constructor của Window)
            if (window.DataContext is DeckRenameViewModel viewModel)
            {
                // 3. Truyền Deck cần sửa vào ViewModel
                // (Hàm Initialize này nằm trong DeckRenameViewModel mà chúng ta vừa tạo)
                viewModel.Initialize(deck);
            }

            // 4. Hiển thị cửa sổ dạng Dialog (chặn cửa sổ chính)
            window.ShowDialog();
        }

        // Thực thi việc mở cửa sổ Học
        public void ShowStudyWindow(string deckId)
        {
            var window = _serviceProvider.GetRequiredService<StudyWindow>();

            if (window.DataContext is not StudyViewModel viewModel)
            {
                Debug.WriteLine("LỖI NGHIÊM TRỌNG: StudyWindow không có StudyViewModel trong DataContext!");
                return;
            }

            window.Loaded += async (sender, e) =>
            {
                try
                {
                    await viewModel.InitializeAsync(deckId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi tải thẻ học: {ex.Message}");
                    MessageBox.Show("Không thể tải bộ thẻ. Vui lòng thử lại.", "Lỗi");
                    window.Close();
                }
            };

            // Thay vì window.Show(), dùng ShowDialog() để chờ cửa sổ đóng lại
            window.ShowDialog();

            // --- SAU KHI CỬA SỔ ĐÓNG ---
            // Gửi tin nhắn báo hiệu đã học xong để MainViewModel cập nhật lại số liệu
            _messenger.Send(new StudySessionCompletedMessage(deckId));
        }

        public void ImportFileWindow()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Import";
            openFileDialog.Filter = "Zip file (*.zip)|*.zip";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Debug.WriteLine($"File to import: {selectedFilePath}");
                // TODO: Gọi service để xử lý file (ví dụ: _importService.Import(selectedFilePath))
            }
            else
            {
                // Người dùng đã nhấn "Cancel"
            }
        }
        public void ShowSyncWindow()
        {
            var window = _serviceProvider.GetRequiredService<SyncWindow>();
            window.ShowDialog();
        }

        public void ShowDeckChosenWindow(string deckId)
        {
            // Dùng GetRequiredService để phát hiện lỗi cấu hình DI sớm nếu thiếu đăng ký
            var window = _serviceProvider.GetRequiredService<DeckChosenWindow>();

            // Lấy ViewModel đã được DI tiêm vào DataContext (DeckChosenWindow ctor phải gán DataContext = viewModel)
            if (window.DataContext is DeckChosenViewModel viewModel)
            {
                // Khởi tạo dữ liệu bất đồng bộ an toàn khi cửa sổ Loaded
                window.Loaded += async (sender, e) =>
                {
                    try
                    {
                        await viewModel.InitializeAsync(deckId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi tải thống kê deck: {ex.Message}");
                        MessageBox.Show("Không thể tải thống kê bộ thẻ. Vui lòng thử lại.", "Lỗi");
                        window.Close();
                    }
                };
            }
            else
            {
                Debug.WriteLine("DeckChosenWindow.DataContext không phải là DeckChosenViewModel!");
            }
            if (Application.Current.MainWindow != null && window != Application.Current.MainWindow)
            {
                window.Owner = Application.Current.MainWindow;
            }
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Chặn cửa sổ chính khi cửa sổ con đang mở
            window.ShowDialog();
        }
        public void ShowRegisterWindow()
        {
            var window = _serviceProvider.GetRequiredService<RegisterWindow>();
            window.Show();
            // Không dùng ShowDialog() nếu muốn đóng cửa sổ cũ ngay lập tức
        }

        public void ShowLoginWindow()
        {
            var window = _serviceProvider.GetRequiredService<LoginWindow>();
            window.Show();
        }
        public void ShowMainWindow()
        {
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            window.Show();
        }
        public void OpenSyncWindow()
        {
            var syncWindow = _serviceProvider.GetRequiredService<SyncWindow>();
            if (Application.Current.MainWindow != null)
            {
                syncWindow.Owner = Application.Current.MainWindow;
            }
            syncWindow.ShowDialog();
        }
        public void ShowOtpWindow(string email)
        {
            var window = _serviceProvider.GetRequiredService<OtpWindow>();
            if (window.DataContext is OtpViewModel viewModel)
            {
                // Giả sử OtpViewModel có một phương thức Initialize để nhận email
                viewModel = new OtpViewModel(
                    _serviceProvider.GetRequiredService<IAuthService>(),
                    this,
                    email);
                window.DataContext = viewModel;
            }
            window.ShowDialog();
        }
        public void ShowResetPasswordWindow()
        {
            var window = _serviceProvider.GetRequiredService<ResetPasswordWindow>();
            window.Show();
        }
        public void ShowJoinWindow()
        {
            // Cửa sổ trung gian (Chọn tạo hoặc nhập mã)
            var window = _serviceProvider.GetRequiredService<JoinWindow>();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        public void ShowCreateRoomWindow()
        {
            // Cửa sổ setting của giáo viên
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        // [QUAN TRỌNG] Hàm mở Lobby nhận tham số
        public void ShowLobbyWindow(string roomId, bool isHost, Deck deck = null, int maxPlayers = 30, int waitTime = 300)
        {
            var window = _serviceProvider.GetRequiredService<LobbyWindow>();

            // Lấy ViewModel để truyền dữ liệu
            if (window.DataContext is LobbyViewModel vm)
            {
                // Gọi hàm InitializeAsync mà chúng ta đã viết ở bước trước
                // Lưu ý: Vì là async void (fire-and-forget) hoặc cần wrap trong Task.Run nếu muốn đợi
                _ = vm.InitializeAsync(roomId, isHost, deck, maxPlayers, waitTime);
            }

            if (Application.Current.MainWindow != null)
                window.Owner = Application.Current.MainWindow;

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowDialog(); // Chặn cửa sổ chính
        }
        public async Task ShowGameWindowAsync(string roomId, string classroomId, Deck selectedDeck, int maxPlayers = 30, int timePerRound = 15)
        {
            var window = _serviceProvider.GetRequiredService<GameWindow>();

            if (window.DataContext is GameViewModel vm)
            {
                await vm.InitializeAsync(roomId, classroomId, selectedDeck, maxPlayers, timePerRound);
            }

            if (Application.Current.MainWindow != null)
                window.Owner = Application.Current.MainWindow;

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowDialog();
        }


    }
}