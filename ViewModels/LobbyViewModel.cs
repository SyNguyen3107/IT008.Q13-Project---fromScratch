using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyFlips.ViewModels
{
    public partial class LobbyViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly UserSession _userSession;
        private readonly IDeckRepository _deckRepository;
        private readonly IClassroomRepository _classroomRepository;
        private readonly SupabaseService _supabaseService;

        // [POLLING] Timer để cập nhật dữ liệu định kỳ
        private DispatcherTimer _syncTimer;

        [ObservableProperty] private string _roomId;
        private string _realClassroomIdUUID;

        [ObservableProperty] private string _currentUserName;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsStudent))] private bool _isHost = false;
        public bool IsStudent => !IsHost;
        public bool CanCloseWindow { get; set; } = false;

        private DispatcherTimer _autoStartTimer;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(AutoStartStatus))] private int _autoStartSeconds;
        [ObservableProperty] private int _totalWaitTime;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(AutoStartStatus))] private bool _isAutoStartActive;
        public string AutoStartStatus => $"Starting in: {TimeSpan.FromSeconds(AutoStartSeconds):mm\\:ss}";

        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();
        [ObservableProperty] private Deck _selectedDeck;

        public string MaxPlayersString => $"/ {MaxPlayers}";
        [ObservableProperty][NotifyPropertyChangedFor(nameof(MaxPlayersString))] private int _maxPlayers = 30;
        [ObservableProperty] private int _timePerRound = 15;

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        public LobbyViewModel(
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _userSession = userSession;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;
            _supabaseService = supabaseService;

            CurrentUserName = !string.IsNullOrEmpty(_userSession.UserName) ? _userSession.UserName : "Guest";

            _autoStartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoStartTimer.Tick += AutoStart_Tick;
        }

        public async Task InitializeAsync(string roomId, bool isHost, Deck deck = null, int? maxPlayers = null, int? waitTime = null)
        {
            RoomId = roomId;
            IsHost = isHost;

            try
            {
                var roomInfo = await _classroomRepository.GetClassroomByCodeAsync(RoomId);
                if (roomInfo == null)
                {
                    MessageBox.Show("Phòng không tồn tại!");
                    ForceCloseWindow();
                    return;
                }

                _realClassroomIdUUID = roomInfo.Id;

                // Load settings ban đầu
                MaxPlayers = maxPlayers ?? roomInfo.MaxPlayers;
                TimePerRound = roomInfo.TimePerRound > 0 ? roomInfo.TimePerRound : 15;
                TotalWaitTime = roomInfo.WaitTime;
                AutoStartSeconds = roomInfo.WaitTime;

                if (IsHost)
                {
                    var decks = await _deckRepository.GetAllAsync();
                    AvailableDecks.Clear();
                    foreach (var d in decks) AvailableDecks.Add(d);
                    SelectedDeck = deck ?? decks.FirstOrDefault();

                    if (waitTime.HasValue && waitTime.Value > 0)
                    {
                        IsAutoStartActive = true;
                        _autoStartTimer.Start();
                    }
                }
                else
                {
                    // Student tự động join vào members table
                    var myId = _authService.CurrentUserId ?? _userSession.UserId;
                    // Gọi repository để join (hoặc SupabaseService)
                    await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);
                }

                // Load danh sách thành viên lần đầu
                await RefreshLobbyState();

                // [POLLING] Bắt đầu vòng lặp kiểm tra (3 giây/lần)
                StartPolling();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo phòng: {ex.Message}");
                ForceCloseWindow();
            }
        }

        // --- CƠ CHẾ POLLING ---
        private void StartPolling()
        {
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromSeconds(3); // Cập nhật mỗi 3 giây
            _syncTimer.Tick += async (s, e) => await RefreshLobbyState();
            _syncTimer.Start();
        }

        private void StopPolling()
        {
            _syncTimer?.Stop();
        }

        private async Task RefreshLobbyState()
        {
            try
            {
                // 1. Kiểm tra xem phòng còn tồn tại không
                var room = await _supabaseService.GetClassroomAsync(_realClassroomIdUUID);

                // Nếu phòng null hoặc không active -> Bị giải tán
                if (room == null || !room.IsActive)
                {
                    StopPolling();
                    MessageBox.Show("Chủ phòng đã giải tán phòng chơi.", "Thông báo");
                    ForceCloseWindow();
                    return;
                }

                // Cập nhật Settings nếu có thay đổi từ Host
                if (MaxPlayers != room.MaxPlayers) MaxPlayers = room.MaxPlayers;
                if (TimePerRound != room.TimePerRound) TimePerRound = room.TimePerRound;

                // Nếu WaitTime đổi thì cập nhật timer
                if (room.WaitTime != TotalWaitTime)
                {
                    TotalWaitTime = room.WaitTime;
                    if (!IsHost && IsAutoStartActive) AutoStartSeconds = TotalWaitTime;
                }

                // 2. Cập nhật danh sách thành viên
                var currentMembers = await _supabaseService.GetClassroomMembersWithProfileAsync(_realClassroomIdUUID);
                UpdatePlayerList(currentMembers);

                // 3. (Tuỳ chọn) Kiểm tra trạng thái Game Start
                if (room.Status == "PLAYING" && !IsHost)
                {
                    // Logic chuyển màn hình game ở đây
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling Error: {ex.Message}");
            }
        }

        private void UpdatePlayerList(List<MemberWithProfile> serverMembers)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Tìm những người cần xóa (có trong UI nhưng không có trong Server)
                var idsFromServer = serverMembers.Select(x => x.UserId).ToHashSet();
                var playersToRemove = Players.Where(p => !idsFromServer.Contains(p.Id)).ToList();

                foreach (var p in playersToRemove)
                {
                    Players.Remove(p);
                }

                // Tìm những người cần thêm (có trong Server nhưng chưa có trong UI)
                foreach (var member in serverMembers)
                {
                    if (!Players.Any(p => p.Id == member.UserId))
                    {
                        Players.Add(new PlayerInfo
                        {
                            Id = member.UserId,
                            Name = member.DisplayName ?? "Unknown",
                            // [FIX] Xử lý Avatar null để tránh lỗi WPF
                            AvatarUrl = !string.IsNullOrEmpty(member.AvatarUrl) ? member.AvatarUrl : "/Images/default_user.png",
                            IsHost = (member.Role == "owner" || member.Role == "host")
                        });
                    }
                }
            });
        }

        private void AutoStart_Tick(object sender, EventArgs e)
        {
            if (AutoStartSeconds > 0)
            {
                AutoStartSeconds--;
            }
            else
            {
                StopAutoStart();
                if (IsHost)
                {
                    StartGame(); // Gọi trực tiếp hàm StartGame
                }
            }
        }

        private void StopAutoStart()
        {
            if (_autoStartTimer.IsEnabled)
            {
                _autoStartTimer.Stop();
            }
            IsAutoStartActive = false;
        }

        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Rời phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    StopPolling(); // Dừng polling ngay lập tức

                    if (!IsHost)
                    {
                        var myId = _authService.CurrentUserId ?? _userSession.UserId;
                        await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, myId);
                    }

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Lobby] Leave error: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task CloseRoom()
        {
            if (MessageBox.Show("Giải tán phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    StopPolling();

                    if (IsHost)
                    {
                        // Xóa phòng, database cascade sẽ xóa members -> Polling của Client sẽ bắt được sự kiện này
                        await _classroomRepository.DeleteClassroomAsync(RoomId);
                    }

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Lobby] Close room error: {ex.Message}");
                }
            }
        }

        // [FIX] Đổi tên hàm từ StartGameCommand -> StartGame để tránh lỗi sinh code trùng lặp
        [RelayCommand]
        private void StartGame()
        {
            StopAutoStart();
            StopPolling();

            // TODO: Cập nhật status phòng thành PLAYING trong DB để các Client khác biết
            // _classroomRepository.UpdateStatusAsync(RoomId, "PLAYING");

            MessageBox.Show("Game starting...", "Info");
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            if (!IsHost) return;

            try
            {
                var settingsVm = new SettingsViewModel(_deckRepository, SelectedDeck, MaxPlayers, TimePerRound, TotalWaitTime);
                var settingsWindow = new Views.SettingsWindow(settingsVm);
                var result = settingsWindow.ShowDialog();

                if (result == true)
                {
                    // Cập nhật vào DB
                    await _classroomRepository.UpdateClassroomSettingsAsync(
                        _realClassroomIdUUID,
                        settingsVm.SelectedDeck?.Id,
                        settingsVm.MaxPlayers,
                        settingsVm.TimePerRound,
                        settingsVm.WaitTimeMinutes * 60
                    );

                    // Polling sẽ tự động cập nhật UI cho mọi người sau tối đa 3s
                    MessageBox.Show("Room settings updated successfully.", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening settings: {ex.Message}", "Error");
            }
        }

        private void ForceCloseWindow()
        {
            _autoStartTimer?.Stop();
            StopPolling();

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            });
        }
    }
}