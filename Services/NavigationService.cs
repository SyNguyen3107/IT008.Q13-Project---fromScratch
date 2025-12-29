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
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessenger _messenger;

        public NavigationService(IServiceProvider serviceProvider, IMessenger messenger)
        {
            _serviceProvider = serviceProvider;
            _messenger = messenger;
            _messenger = messenger;
        }

        public void ShowAddCardWindow()
        {
            var window = _serviceProvider.GetRequiredService<AddCardWindow>();
            window.ShowDialog();
        }

        public void ShowCardWindow()
        {
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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Import";
            openFileDialog.Filter = "Zip file (*.zip)|*.zip";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Debug.WriteLine($"File to import: {selectedFilePath}");
            }
            else
            {
            }
        }
        public void ShowSyncWindow()
        {
            var window = _serviceProvider.GetRequiredService<SyncWindow>();
            window.ShowDialog();
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
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        public void ShowCreateRoomWindow()
        {
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        public void ShowLobbyWindow(string roomId, bool isHost, Deck deck = null, int maxPlayers = 30, int waitTime = 300)
        {
            var window = _serviceProvider.GetRequiredService<LobbyWindow>();

            if (window.DataContext is LobbyViewModel vm)
            {
                _ = vm.InitializeAsync(roomId, isHost, deck, maxPlayers, waitTime);
            }

            if (Application.Current.MainWindow != null)
                window.Owner = Application.Current.MainWindow;

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowDialog();
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
        public async Task ShowCreateRoomWindowAsync()
        {
            var vm = _serviceProvider.GetRequiredService<CreateRoomViewModel>();
            var window = _serviceProvider.GetRequiredService<CreateRoomWindow>();
            window.DataContext = vm;
            window.Show();
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
        public void ShowMainWindow()
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                if (existingWindow != null)
                {
                    existingWindow.Show();
                    if (existingWindow.WindowState == WindowState.Minimized)
                        existingWindow.WindowState = WindowState.Normal;
                    existingWindow.Activate();
                }
                else
                {
                    var newMain = _serviceProvider.GetRequiredService<MainWindow>();
                    newMain.Show();
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

            try
            {
                await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Init error: {ex.Message}");
            }
        }


        public async Task ShowMemberGameWindowAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            var vm = _serviceProvider.GetRequiredService<MemberGameViewModel>();

            await vm.InitializeAsync(roomId, classroomId, deck, timePerRound);

            var window = _serviceProvider.GetRequiredService<MemberGameWindow>();
            window.DataContext = vm;
            window.Show();

            CloseSpecificWindows(typeof(MemberLobbyWindow));
        }
        private void CloseSpecificWindows(params Type[] windowTypesToClose)
        {
            foreach (Window window in Application.Current.Windows.OfType<Window>().ToList())
            {
                if (windowTypesToClose.Contains(window.GetType()))
                {
                    window.Close();
                }
            }
        }
        public void ShowLeaderBoardWindow(
            string roomId = null,
            string classroomId = null,
            IEnumerable<PlayerInfo> players = null)
                {
                    var currentWindow = Application.Current.Windows
            .OfType<Window>()
            .SingleOrDefault(w => w.IsActive);

            currentWindow?.Close();

            var window = _serviceProvider.GetRequiredService<LeaderBoardWindow>();

            if (window.DataContext is LeaderBoardViewModel vm)
            {
                vm.Initialize(roomId, classroomId, players ?? Enumerable.Empty<PlayerInfo>());
            }

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
        }



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
    }
}