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
using System.Linq;

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
            var window = _serviceProvider.GetRequiredService<JoinWindow>();

            // [FIX 2] Kiểm tra an toàn trước khi gán Owner
            // Lấy cửa sổ MainWindow thực sự đang active
            var mainWindow = Application.Current.MainWindow;

            // Chỉ gán Owner nếu MainWindow tồn tại, đang hiển thị, và KHÔNG PHẢI là chính cái cửa sổ Join này
            if (mainWindow != null && mainWindow.IsVisible && mainWindow != window)
            {
                window.Owner = mainWindow;
            }
            else
            {
                // Nếu không tìm thấy Owner hợp lệ, căn giữa màn hình Desktop
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowDialog();
        }

        public void ShowCreateRoomWindow()
        {
            // Cửa sổ setting của giáo viên
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }
        public async Task ShowCreateRoomWindowAsync()
        {
            var vm = _serviceProvider.GetRequiredService<CreateRoomViewModel>();
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.DataContext = vm;
            window.Show();
            // Không đóng JoinWindow ở đây ngay để tránh ứng dụng bị tắt nếu shutdown mode là OnLastWindowClose
            // Ta sẽ đóng nó sau khi window mới hiện lên (Logic CloseOldWindows)
            CloseSpecificWindows(typeof(JoinWindow), typeof(MainWindow));
        }

        public async Task ShowHostLobbyWindowAsync(string roomId)
        {
            var vm = _serviceProvider.GetRequiredService<HostLobbyViewModel>();
            await vm.InitializeAsync(roomId);

            var window = _serviceProvider.GetRequiredService<HostLobbyWindow>();
            window.DataContext = vm;
            window.Show();

            CloseSpecificWindows(typeof(CreateRoomWindow), typeof(JoinWindow), typeof(MainWindow));
        }

        public async Task ShowMemberLobbyWindowAsync(string roomId)
        {
            var vm = _serviceProvider.GetRequiredService<MemberLobbyViewModel>();
            await vm.InitializeAsync(roomId);

            var window = _serviceProvider.GetRequiredService<MemberLobbyWindow>();
            window.DataContext = vm;
            window.Show();

            CloseSpecificWindows(typeof(JoinWindow), typeof(MainWindow));
        }
        /// <summary>
        /// Mở lại MainWindow (Gọi khi rời phòng)
        /// </summary>
        public void ShowMainWindow()
        {
            // Kiểm tra an toàn
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Tìm MainWindow đã có sẵn
                    var existingWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                    if (existingWindow != null)
                    {
                        existingWindow.Show();
                        if (existingWindow.WindowState == WindowState.Minimized)
                            existingWindow.WindowState = WindowState.Normal;
                        existingWindow.Activate();

                        // [QUAN TRỌNG 1] Cập nhật lại biến hệ thống để WPF biết đây là cửa sổ chính
                        // Điều này giúp hàm ShowJoinWindow lấy đúng Owner
                        Application.Current.MainWindow = existingWindow;
                    }
                    else
                    {
                        // Tạo mới nếu chưa có
                        var newMain = _serviceProvider.GetRequiredService<MainWindow>();
                        newMain.Show();

                        // [QUAN TRỌNG 1] Gán cửa sổ mới tạo làm MainWindow hệ thống
                        Application.Current.MainWindow = newMain;
                    }

                    // [QUAN TRỌNG 2] Đóng tất cả các cửa sổ vệ tinh
                    // Nếu thiếu đoạn này, cửa sổ Game/Leaderboard sẽ không bao giờ tắt!
                    CloseSpecificWindows(
                        typeof(HostGameWindow),
                        typeof(MemberGameWindow),
                        typeof(HostLobbyWindow),
                        typeof(MemberLobbyWindow),
                        typeof(JoinWindow),
                        typeof(CreateRoomWindow),
                        typeof(HostLeaderboardWindow),
                        typeof(MemberLeaderboardWindow)
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing MainWindow: {ex.Message}");
                }
            });
        }
        public async Task ShowHostGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            HostGameViewModel vm;
            try
            {
                vm = _serviceProvider.GetRequiredService<HostGameViewModel>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Resolve error: {ex.Message}");
                return;
            }

            HostGameWindow window;
            try
            {
                window = _serviceProvider.GetRequiredService<HostGameWindow>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Window resolve error: {ex.Message}");
                return;
            }

            window.DataContext = vm;
            Application.Current.MainWindow = window;
            window.Show();

            CloseSpecificWindows(typeof(HostLobbyWindow));

            // Gọi InitializeAsync SAU khi Window đã hiển thị
            try
            {
                await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Init error: {ex.Message}");
                // Nếu lỗi, vẫn giữ Window mở để người dùng thấy thông báo
            }
        }


        public async Task ShowMemberGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            var vm = _serviceProvider.GetRequiredService<MemberGameViewModel>();

            // Lưu ý: Member có thể chưa có Deck ngay lúc này nếu logic tải Deck nằm ở Lobby
            // Nếu Deck null, MemberViewModel sẽ phải tự tải lại từ API trong InitializeAsync
            await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);

            var window = _serviceProvider.GetRequiredService<MemberGameWindow>();
            window.DataContext = vm;
            window.Show();

            // 3. Đóng Lobby (MemberLobbyWindow)
            CloseSpecificWindows(typeof(MemberLobbyWindow));
        }
        public void ShowHostLeaderboardWindow(string roomId, string classroomId, List<PlayerInfo> finalResults)
        {
            // 1. Lấy ViewModel từ DI
            var vm = _serviceProvider.GetRequiredService<HostLeaderboardViewModel>();

            // 2. Gọi Initialize (Lưu ý: Hàm này trong VM nên là async void hoặc fire-and-forget task vì ShowWindow là void)
            // Hoặc tốt nhất: Gọi đồng bộ phần data, phần async để chạy ngầm
            _ = vm.InitializeAsync(roomId, classroomId, finalResults);

            // 3. Tạo Window
            var window = _serviceProvider.GetRequiredService<HostLeaderboardWindow>();
            window.DataContext = vm;
            window.Show();

            //// 4. Đóng cửa sổ Game cũ
            CloseSpecificWindows(typeof(HostGameWindow));
        }

        public void ShowMemberLeaderboardWindow(string roomId, string classroomId)
        {
            var vm = _serviceProvider.GetRequiredService<MemberLeaderboardViewModel>();

            _ = vm.InitializeAsync(roomId, classroomId);

            var window = _serviceProvider.GetRequiredService<MemberLeaderboardWindow>();
            window.DataContext = vm;
            window.Show();

            
            CloseSpecificWindows(typeof(MemberGameWindow));
        }

        /// <summary>
        /// Hàm Helper: Đóng các cửa sổ thuộc các loại được chỉ định
        /// </summary>
        private void CloseSpecificWindows(params Type[] windowTypesToClose)
        {
            // Dùng ToList() để tạo bản sao danh sách cửa sổ trước khi duyệt để tránh lỗi collection modified
            foreach (Window window in Application.Current.Windows.OfType<Window>().ToList())
            {
                // Nếu cửa sổ hiện tại nằm trong danh sách cần đóng -> Đóng nó
                if (windowTypesToClose.Contains(window.GetType()))
                {
                    window.Close();
                }
            }
        }
        public void CloseCurrentWindow()
        {
            // Logic đóng cửa sổ hiện tại an toàn
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive) // Hoặc logic xác định cửa sổ nào cần đóng
                {
                    window.Close(); 
                    // Cẩn thận: Nếu đóng MainWindow thì app sẽ tắt. 
                    // Thường ta chỉ đóng cửa sổ "trước đó" sau khi cửa sổ mới đã hiện.
                    // Bạn có thể để ViewModel tự gọi ForceCloseWindow() như code ở phần trước.
                }
            }
        }

        public void NavigateToDashboard()
        {
            // Tìm đúng cửa sổ chính đang mở
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                // 1. MỞ: Hiện Frame lên (nó sẽ che toàn bộ Sidebar và nội dung chính)
                mainWindow.MainFrame.Visibility = Visibility.Visible;

                // 2. Chèn trang Dashboard vào
                var dashboardPage = _serviceProvider.GetRequiredService<DashBoardWinDow>();
                mainWindow.MainFrame.Navigate(dashboardPage);
            }
        }

        public void NavigateToHome()
        {
            // Lấy cửa sổ chính đang hiển thị
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

            if (mainWindow != null)
            {
                // Phải dùng Dispatcher nếu bạn gọi từ một Thread khác, 
                // nhưng thông thường chỉ cần:
                mainWindow.MainFrame.Visibility = Visibility.Collapsed;
                mainWindow.MainFrame.Content = null;
            }
        }
    }
}