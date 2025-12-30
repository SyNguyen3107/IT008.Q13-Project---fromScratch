using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Messages;
using EasyFlips.Models;
using EasyFlips.ViewModels;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;

        public NavigationService(IServiceProvider serviceProvider, IMessenger messenger)
        {
            _serviceProvider = serviceProvider;
            _messenger = messenger;
        }

        #region Card & Deck Management

        public void ShowAddCardWindow()
        {
            var window = _serviceProvider.GetRequiredService<AddCardWindow>();
            window.ShowDialog();
        }

        public void ShowCardWindow()
        {
            // Chưa có implement
        }

        public void ShowCreateDeckWindow()
        {
            var window = _serviceProvider.GetRequiredService<CreateDeckWindow>();
            window.ShowDialog();
        }

        public void ShowDeckRenameWindow(Deck deck)
        {
            var window = _serviceProvider.GetRequiredService<DeckRenameWindow>();
            if (window.DataContext is DeckRenameViewModel viewModel)
            {
                viewModel.Initialize(deck);
            }
            window.ShowDialog();
        }

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
                    MessageBox.Show("Failed to load the deck. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    window.Close();
                }
            };

            window.ShowDialog();
            _messenger.Send(new StudySessionCompletedMessage(deckId));
        }

        public void ImportFileWindow()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Import",
                Filter = "Zip file (*.zip)|*.zip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Debug.WriteLine($"File to import: {openFileDialog.FileName}");
            }
        }

        public void ShowDeckChosenWindow(string deckId)
        {
            var window = _serviceProvider.GetRequiredService<DeckChosenWindow>();

            if (window.DataContext is DeckChosenViewModel viewModel)
            {
                window.Loaded += async (sender, e) =>
                {
                    try
                    {
                        await viewModel.InitializeAsync(deckId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi tải thống kê deck: {ex.Message}");
                        MessageBox.Show("Failed to load deck statistics. Please try again.", "Error");
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
            window.ShowDialog();
        }

        #endregion

        #region Auth & Sync

        public void ShowSyncWindow()
        {
            var window = _serviceProvider.GetRequiredService<SyncWindow>();
            window.ShowDialog();
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

        public void ShowRegisterWindow()
        {
            var window = _serviceProvider.GetRequiredService<RegisterWindow>();
            window.Show();
        }

        public void ShowLoginWindow()
        {
            var window = _serviceProvider.GetRequiredService<LoginWindow>();
            window.Show();
        }

        public void ShowOtpWindow(string email)
        {
            var window = _serviceProvider.GetRequiredService<OtpWindow>();
            // Logic tạo VM thủ công này có thể thay bằng DI nếu đăng ký đúng cách, nhưng giữ nguyên theo code cũ
            if (window.DataContext is OtpViewModel)
            {
                var viewModel = new OtpViewModel(
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

        #endregion

        #region Game Flow Navigation (Host & Member)

        public void ShowMainWindow()
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var existingWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                    if (existingWindow != null)
                    {
                        existingWindow.Show();
                        if (existingWindow.WindowState == WindowState.Minimized)
                            existingWindow.WindowState = WindowState.Normal;
                        existingWindow.Activate();
                        Application.Current.MainWindow = existingWindow;
                    }
                    else
                    {
                        var newMain = _serviceProvider.GetRequiredService<MainWindow>();
                        newMain.Show();
                        Application.Current.MainWindow = newMain;
                    }

                    // Đóng tất cả các cửa sổ game/lobby cũ
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
                    Debug.WriteLine($"Error showing MainWindow: {ex.Message}");
                }
            });
        }

        public void ShowJoinWindow()
        {
            var window = _serviceProvider.GetRequiredService<JoinWindow>();
            var mainWindow = Application.Current.MainWindow;

            if (mainWindow != null && mainWindow.IsVisible && mainWindow != window)
            {
                window.Owner = mainWindow;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowDialog();
        }

        public void ShowCreateRoomWindow()
        {
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            if (Application.Current.MainWindow != null)
                window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        public async Task ShowCreateRoomWindowAsync()
        {
            var vm = _serviceProvider.GetRequiredService<CreateRoomViewModel>();
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.DataContext = vm;
            window.Show();
            CloseSpecificWindows(typeof(JoinWindow), typeof(MainWindow));
            await Task.CompletedTask;
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

        public async Task ShowHostGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<HostGameViewModel>();
                var window = _serviceProvider.GetRequiredService<HostGameWindow>();

                window.DataContext = vm;
                // Gán MainWindow để tránh lỗi Owner
                Application.Current.MainWindow = window;
                window.Show();

                CloseSpecificWindows(typeof(HostLobbyWindow));

                await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Host Game Error: {ex.Message}");
            }
        }

        public async Task ShowMemberGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<MemberGameViewModel>();
                var window = _serviceProvider.GetRequiredService<MemberGameWindow>();

                window.DataContext = vm;
                window.Show();

                CloseSpecificWindows(typeof(MemberLobbyWindow));

                await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Member Game Error: {ex.Message}");
            }
        }

        public void ShowHostLeaderboardWindow(string roomId, string classroomId, List<PlayerInfo> finalResults)
        {
            var vm = _serviceProvider.GetRequiredService<HostLeaderboardViewModel>();
            _ = vm.InitializeAsync(roomId, classroomId, finalResults);

            var window = _serviceProvider.GetRequiredService<HostLeaderboardWindow>();
            window.DataContext = vm;
            window.Show();

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

        #endregion

        #region Helpers & Common Navigation

        public void CloseCurrentWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive)
                {
                    window.Close();
                }
            }
        }

        /// <summary>
        /// Hàm Helper: Đóng các cửa sổ thuộc các loại được chỉ định
        /// </summary>
        private void CloseSpecificWindows(params Type[] windowTypes)
        {
            if (Application.Current == null) return;

            // Dùng ToList() để tránh lỗi modified collection khi đang đóng
            var windowsToClose = Application.Current.Windows.OfType<Window>().ToList();

            foreach (var window in windowsToClose)
            {
                if (windowTypes.Contains(window.GetType()))
                {
                    window.Close();
                }
            }
        }

        public void NavigateToDashboard()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Visibility = Visibility.Visible;
                var dashboardPage = _serviceProvider.GetRequiredService<DashBoardWinDow>();
                mainWindow.MainFrame.Navigate(dashboardPage);
            }
        }

        public void NavigateToHome()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Visibility = Visibility.Collapsed;
                mainWindow.MainFrame.Content = null;
            }
        }

        #endregion
    }
}