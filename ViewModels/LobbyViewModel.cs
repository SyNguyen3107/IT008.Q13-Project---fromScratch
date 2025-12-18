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
        private DateTime _lastUpdatedAt = DateTime.MinValue;


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
                // Nếu server có lưu thời điểm bắt đầu auto start
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
                    var updatedUtc = DateTime.SpecifyKind(roomInfo.UpdatedAt, DateTimeKind.Utc);
                    var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Room UpdatedAt (UTC): {updatedUtc}, Now (UTC): {DateTime.Now}, Elapsed: {elapsed}s");
                    _lastUpdatedAt = roomInfo.UpdatedAt;
                    AutoStartSeconds = Math.Max(roomInfo.WaitTime - elapsed, 0);



                    if (waitTime.HasValue && waitTime.Value > 0)
                    {
                        IsAutoStartActive = true;
                        _autoStartTimer.Start();
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] AutoStartActive={IsAutoStartActive}, AutoStartSeconds={AutoStartSeconds}, TotalWaitTime={TotalWaitTime}, TimerEnabled={_autoStartTimer.IsEnabled}");
                    }
                    else
                    {
                        MessageBox.Show("⏰ Hết giờ, bạn không thể join vào phòng này nữa.", "Thông báo");
                        AutoStartSeconds = 0;
                        IsAutoStartActive = false;
                        CanCloseWindow = true;
                        ForceCloseWindow();
                    }
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

        // Định nghĩa thời gian timeout (Ví dụ: 15 giây không phản hồi thì kick)
        private const int HEARTBEAT_TIMEOUT_SECONDS = 15;

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
                    CanCloseWindow = true;
                    ForceCloseWindow();
                    return;
                }

                // Cập nhật Settings nếu có thay đổi từ Host
                if (MaxPlayers != room.MaxPlayers) MaxPlayers = room.MaxPlayers;
                if (TimePerRound != room.TimePerRound) TimePerRound = room.TimePerRound;

                // Logic đồng bộ thời gian (Giữ nguyên theo code bạn gửi)
                if (room.UpdatedAt != _lastUpdatedAt && !IsHost)
                {
                    _lastUpdatedAt = room.UpdatedAt;
                    TotalWaitTime = room.WaitTime;
                    var updatedUtc = DateTime.SpecifyKind(room.UpdatedAt, DateTimeKind.Utc);
                    var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;
                    AutoStartSeconds = Math.Max(room.WaitTime - elapsed, 0);
                }

                var myId = _authService.CurrentUserId ?? _userSession.UserId;

                if (!IsHost)
                {
                    // Nếu là Member: Gửi tín hiệu "Tôi còn sống"
                    // Fire and forget để không chặn UI
                    _ = _supabaseService.SendHeartbeatAsync(_realClassroomIdUUID, myId);
                }

                // 2. Cập nhật danh sách thành viên
                // Lưu ý: Đảm bảo MemberWithProfile đã có cột LastActive
                var currentMembers = await _supabaseService.GetClassroomMembersWithProfileAsync(_realClassroomIdUUID);

                // --- [START] LOGIC HOST TỰ ĐỘNG KICK NGƯỜI MẤT KẾT NỐI ---
                if (IsHost)
                {
                    var now = DateTime.UtcNow;
                    var usersToKick = new List<string>();

                    foreach (var member in currentMembers)
                    {
                        // Bỏ qua chính mình (Host)
                        if (member.UserId == myId) continue;

                        // Kiểm tra LastActive
                        // Nếu LastActive quá cũ (quá 15s so với hiện tại) -> Cho vào danh sách kick
                        // Lưu ý: Nếu LastActive == MinValue (mới vào chưa kịp update) thì tạm tha
                        if (member.LastActive != DateTime.MinValue)
                        {
                            // Chuyển đổi về UTC chuẩn để so sánh
                            var lastActiveUtc = member.LastActive.Kind == DateTimeKind.Unspecified
                                                ? DateTime.SpecifyKind(member.LastActive, DateTimeKind.Utc)
                                                : member.LastActive.ToUniversalTime();

                            if ((now - lastActiveUtc).TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
                            {
                                usersToKick.Add(member.UserId);
                            }
                        }
                    }

                    // Thực hiện Kick
                    foreach (var userId in usersToKick)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoKick] Kicking user: {userId}");
                        // Gọi API xóa thành viên
                        await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, userId);

                        // Xóa luôn khỏi danh sách hiển thị tạm thời để UI cập nhật ngay lập tức
                        var memberToRemove = currentMembers.FirstOrDefault(x => x.UserId == userId);
                        if (memberToRemove != null)
                        {
                            currentMembers.Remove(memberToRemove);
                        }
                    }
                }
                // --- [END] LOGIC HOST TỰ ĐỘNG KICK ---

                // Cập nhật danh sách lên UI
                UpdatePlayerList(currentMembers);

                // 3. (Tuỳ chọn) Kiểm tra trạng thái Game Start
                if (room.Status == "PLAYING" && !IsHost)
                {
                    _navigationService.ShowGameWindowAsync(
                            RoomId,
                            _realClassroomIdUUID,
                            SelectedDeck,
                            MaxPlayers,
                            TimePerRound
                         );
                    MessageBox.Show("Game Started!"); // Ví dụ
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
        private async Task StartGame()
        {
            try
            {
                StopAutoStart();
                StopPolling();

                await _classroomRepository.UpdateStatusAsync(_realClassroomIdUUID, "PLAYING");

                _navigationService.ShowGameWindowAsync(
                            RoomId,
                            _realClassroomIdUUID,
                            SelectedDeck,
                            MaxPlayers,
                            TimePerRound
                         );

                CanCloseWindow = true;
                ForceCloseWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting game: {ex.Message}", "Error");
            }
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
                    // Cập nhật ngay lập tức cho host
                    TotalWaitTime = settingsVm.WaitTimeMinutes * 60;
                    AutoStartSeconds = TotalWaitTime;
                    MaxPlayers = settingsVm.MaxPlayers;
                    IsAutoStartActive = true;
                    _autoStartTimer.Start();


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