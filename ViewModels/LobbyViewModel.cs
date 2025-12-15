using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Views;
using Newtonsoft.Json.Linq;
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
        private readonly RealtimeService _realtimeService;
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly UserSession _userSession;
        private readonly IDeckRepository _deckRepository;
        private readonly IClassroomRepository _classroomRepository;

        [ObservableProperty] private string _roomId;
        private string _realClassroomIdUUID;

        [ObservableProperty] private string _currentUserName;
        [ObservableProperty] private string _currentUserAvatar;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsStudent))] private bool _isHost = false;
        public bool IsStudent => !IsHost;
        public bool CanCloseWindow { get; set; } = false;

        private DispatcherTimer _timer;
        [ObservableProperty] private bool _isTimerRunning;
        [ObservableProperty] private int _timeRemaining;
        [ObservableProperty] private string _timeRemainingString = "00:00";

        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();
        [ObservableProperty] private Deck _selectedDeck;

        // Property hiển thị chuỗi dạng "/ 30"
        public string MaxPlayersString => $"/ {MaxPlayers}";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxPlayersString))] // Tự động cập nhật chuỗi hiển thị khi số thay đổi
        private int _maxPlayers = 30;

        [ObservableProperty] private int _timePerRound = 15;

        // === Các biến phục vụ Auto Start ===
        private DispatcherTimer _autoStartTimer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private int _autoStartSeconds; // Thời gian đếm ngược (giây)

        [ObservableProperty]
        private int _totalWaitTime; // Tổng thời gian (để tính % ProgressBar)

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private bool _isAutoStartActive; // Cờ để hiện/ẩn UI đếm ngược

        public string AutoStartStatus => $"Starting in: {TimeSpan.FromSeconds(AutoStartSeconds):mm\\:ss}";


        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        private Dictionary<string, DateTime> _lastSeenMap = new Dictionary<string, DateTime>();
        private DispatcherTimer _heartbeatTimer;
        public ObservableCollection<string> DebugLogs { get; } = new ObservableCollection<string>();

        public LobbyViewModel(
            RealtimeService realtimeService,
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository)
        {
            _realtimeService = realtimeService;
            _authService = authService;
            _navigationService = navigationService;
            _userSession = userSession;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;

            CurrentUserName = !string.IsNullOrEmpty(_userSession.UserName) ? _userSession.UserName : "Guest";

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _heartbeatTimer.Tick += Heartbeat_Tick;

            _realtimeService.OnMessageReceived += HandleRealtimeMessage;

            // Khởi tạo Timer cho Auto Start
            _autoStartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoStartTimer.Tick += AutoStart_Tick;
        }

        // [FIX QUAN TRỌNG]: Đổi tham số thành 'int? maxPlayers = null'
        // Để nếu bên ngoài không truyền vào, nó sẽ KHÔNG reset giá trị hiện tại về 30
        public async Task InitializeAsync(string roomId, bool isHost, Deck deck = null, int? maxPlayers = null, int? waitTime = null)
        {
            RoomId = roomId;
            IsHost = isHost;
            CanCloseWindow = false;

            // Chỉ cập nhật nếu có giá trị truyền vào rõ ràng (từ CreateRoomViewModel)
            if (maxPlayers.HasValue)
            {
                MaxPlayers = maxPlayers.Value;
            }
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

                if (roomInfo.TimePerRound > 0)
                {
                    TimePerRound = roomInfo.TimePerRound;
                }

                if (IsHost)
                {
                    var decks = await _deckRepository.GetAllAsync();
                    AvailableDecks.Clear();
                    foreach (var d in decks) AvailableDecks.Add(d);
                    SelectedDeck = deck ?? decks.FirstOrDefault();
                }
                else
                {
                    var myId = _authService.CurrentUserId ?? _userSession.UserId;
                    await _classroomRepository.AddMemberAsync(_realClassroomIdUUID, myId);
                }

                // Xử lý logic WaitTime
                if (isHost && waitTime.HasValue && waitTime.Value > 0)
                {
                    TotalWaitTime = waitTime.Value;
                    AutoStartSeconds = waitTime.Value;
                    IsAutoStartActive = true;

                    _autoStartTimer.Start();
                }
                else
                {
                    IsAutoStartActive = false;
                }


                await ConnectAndJoinRoom();

                _heartbeatTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo phòng: {ex.Message}");
                ForceCloseWindow();
            }
        }

        private async Task ConnectAndJoinRoom()
        {
            try
            {
                var currentMembers = await _classroomRepository.GetMembersAsync(_realClassroomIdUUID);

                Players.Clear();
                foreach (var m in currentMembers)
                {
                    string displayName = m.Profile?.DisplayName;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = $"Player {m.UserId.Substring(Math.Max(0, m.UserId.Length - 4))}";
                    }

                    Players.Add(new PlayerInfo
                    {
                        Id = m.UserId,
                        Name = displayName,
                        AvatarUrl = m.Profile?.AvatarUrl,
                        IsHost = m.Role == "owner"
                    });
                    _lastSeenMap[m.UserId] = DateTime.Now;
                }

                var connectTask = _realtimeService.ConnectAsync();
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask) { }
                else await connectTask;

                var joinTask = _realtimeService.JoinRoomAsync(RoomId);
                if (await Task.WhenAny(joinTask, Task.Delay(5000)) != joinTask) return;
                await joinTask;

                var myInfo = GetMyInfo();
                await _realtimeService.SendMessageAsync("PLAYER_JOIN", myInfo);
            }
            catch { }
        }

        private void HandleRealtimeMessage(string eventName, object data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var json = JObject.FromObject(data);
                    switch (eventName)
                    {
                        case "PLAYER_JOIN": HandlePlayerJoin(json); break;
                        case "HEARTBEAT": HandleHeartbeat(json); break;
                        case "LOBBY_UPDATE": HandleLobbyUpdate(json); break;
                        case "KICK_PLAYER": HandleKickPlayer(json); break;
                        case "CLOSE_ROOM": HandleCloseRoom(); break;
                        case "START_GAME": HandleStartGame(); break;
                        case "TIMER_SYNC": HandleTimerSync(json); break;
                    }
                }
                catch { }
            });
        }

        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Rời phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    if (!IsHost && !string.IsNullOrEmpty(_realClassroomIdUUID))
                    {
                        var myId = _authService.CurrentUserId ?? _userSession.UserId;
                        await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, myId);
                    }
                }
                catch { }

                _realtimeService.SendMessageAsync("KICK_PLAYER", new { id = GetMyInfo().Id });
                CanCloseWindow = true;
                ForceCloseWindow();
            }
        }

        private void HandleHeartbeat(JObject json)
        {
            if (!IsHost) return;
            string userId = json["id"]?.ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                _lastSeenMap[userId] = DateTime.Now;
            }
        }

        private void Heartbeat_Tick(object sender, EventArgs e)
        {
            var myId = _authService.CurrentUserId ?? _userSession.UserId;
            _realtimeService.SendMessageAsync("HEARTBEAT", new { id = myId });
            if (IsHost) CheckForOfflineUsers();
        }

        private void CheckForOfflineUsers()
        {
            var now = DateTime.Now;
            var timeoutLimit = TimeSpan.FromSeconds(10);
            var playersToRemove = new List<PlayerInfo>();

            foreach (var p in Players)
            {
                if (p.IsHost) continue;
                if (_lastSeenMap.ContainsKey(p.Id))
                {
                    if (now - _lastSeenMap[p.Id] > timeoutLimit) playersToRemove.Add(p);
                }
                else _lastSeenMap[p.Id] = DateTime.Now;
            }

            if (playersToRemove.Count > 0)
            {
                foreach (var p in playersToRemove)
                {
                    Players.Remove(p);
                    _lastSeenMap.Remove(p.Id);
                }
                BroadcastLobbyState();
            }
        }

        // Hàm xử lý khi Timer nhảy số
        private void AutoStart_Tick(object sender, EventArgs e)
        {
            if (AutoStartSeconds > 0)
            {
                AutoStartSeconds--;
            }
            else
            {
                // Hết giờ -> Tự động Start
                StopAutoStart();
                //StartGameCommand.Execute(null);
            }
        }
        // Hàm dừng Auto Start (Gọi khi Start thủ công hoặc Rời phòng)
        private void StopAutoStart()
        {
            if (_autoStartTimer.IsEnabled)
            {
                _autoStartTimer.Stop();
            }
            IsAutoStartActive = false;
        }

        private void HandlePlayerJoin(JObject json)
        {
            if (!IsHost) return;
            var newPlayer = json.ToObject<PlayerInfo>();
            if (newPlayer != null)
            {
                _lastSeenMap[newPlayer.Id] = DateTime.Now;
                if (!Players.Any(p => p.Id == newPlayer.Id))
                {
                    Players.Add(newPlayer);
                    BroadcastLobbyState();
                }
            }
        }

        private void HandleLobbyUpdate(JObject json)
        {
            var playersList = json["players"]?.ToObject<List<PlayerInfo>>();
            if (playersList != null)
            {
                Players.Clear();
                foreach (var p in playersList) Players.Add(p);
            }
        }

        private void HandleKickPlayer(JObject json)
        {
            string kickedId = json["id"]?.ToString();
            string myId = _authService.CurrentUserId ?? _userSession.UserId;
            if (kickedId == myId)
            {
                CanCloseWindow = true;
                MessageBox.Show("Bạn đã bị mời ra khỏi phòng!", "Thông báo");
                ForceCloseWindow();
            }
        }

        private void HandleCloseRoom()
        {
            if (!IsHost)
            {
                CanCloseWindow = true;
                MessageBox.Show("Giáo viên đã giải tán phòng học.", "Thông báo");
                ForceCloseWindow();
            }
        }

        private void HandleTimerSync(JObject json)
        {
            if (IsHost) return;
            TimeRemaining = json["seconds"]?.Value<int>() ?? 0;
            IsTimerRunning = json["isRunning"]?.Value<bool>() ?? false;
            TimeRemainingString = TimeSpan.FromSeconds(TimeRemaining).ToString(@"mm\:ss");
        }

        private void HandleStartGame()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CanCloseWindow = true;
                ForceCloseWindow();
            });
        }

        [RelayCommand]
        private void KickPlayer(PlayerInfo player)
        {
            if (!IsHost || player == null) return;
            _realtimeService.SendMessageAsync("KICK_PLAYER", new { id = player.Id });
            Players.Remove(player);
            BroadcastLobbyState();
        }

        [RelayCommand]
        private async Task CloseRoom()
        {
            if (MessageBox.Show("Giải tán phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // [QUAN TRỌNG 1]: Mở khóa cửa sổ NGAY LẬP TỨC
                // Để dù mạng có lỗi, cửa sổ vẫn có quyền đóng lại.
                CanCloseWindow = true;

                try
                {
                    // 1. Xóa dữ liệu trong DB (Ưu tiên cao)
                    if (IsHost)
                    {
                        await _classroomRepository.DeleteClassroomAsync(RoomId);
                    }
                }
                catch (Exception)
                {
                    // Lỗi xóa DB không nên chặn việc đóng App
                }

                try
                {
                    // [QUAN TRỌNG 2]: Gửi tin nhắn với Timeout
                    // Nếu gửi tin nhắn mất quá 2 giây (mạng lag), bỏ qua luôn để đóng Window cho mượt.
                    var sendTask = _realtimeService.SendMessageAsync("CLOSE_ROOM", new { });
                    var timeoutTask = Task.Delay(2000); // Timeout 2s

                    await Task.WhenAny(sendTask, timeoutTask);
                }
                catch
                {
                    // Lỗi Socket cũng không được chặn đóng App
                }

                // 3. Đóng cửa sổ
                ForceCloseWindow();
            }
        }

        [RelayCommand]
        private void StartGameCommand()
        {

            StopAutoStart(); // <--- Thêm dòng này để hủy đếm ngược nếu Host bấm nút sớm

            TimeRemaining = TimePerRound > 0 ? TimePerRound : 15;
            IsTimerRunning = true;
            _timer.Start();
            _realtimeService.SendMessageAsync("TIMER_SYNC", new { seconds = TimeRemaining, isRunning = true });
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            if (!IsHost) return;

            try
            {
                var settingsVm = new SettingsViewModel(_deckRepository, SelectedDeck, MaxPlayers, TimePerRound, TotalWaitTime);
                var settingsWindow = new SettingsWindow(settingsVm);
                var result = settingsWindow.ShowDialog();

                if (result == true)
                {
                    var newDeck = settingsVm.SelectedDeck;
                    var newMax = settingsVm.MaxPlayers;
                    var newTimePerRound = settingsVm.TimePerRound;
                    var newWaitMinutes = settingsVm.WaitTimeMinutes;
                    var newWaitSeconds = newWaitMinutes * 60;

                    if (!string.IsNullOrEmpty(_realClassroomIdUUID))
                    {
                        try
                        {
                            var updated = await _classroomRepository.UpdateClassroomSettingsAsync(_realClassroomIdUUID, newDeck?.Id, newMax, newTimePerRound, newWaitSeconds);
                            if (updated != null)
                            {
                                SelectedDeck = AvailableDecks.FirstOrDefault(d => d.Id == newDeck?.Id) ?? newDeck;
                                MaxPlayers = newMax;
                                TimePerRound = newTimePerRound;

                                TotalWaitTime = newWaitSeconds;
                                if (newWaitSeconds > 0)
                                {
                                    AutoStartSeconds = newWaitSeconds;
                                    IsAutoStartActive = true;
                                    if (!_autoStartTimer.IsEnabled) _autoStartTimer.Start();
                                }
                                else
                                {
                                    StopAutoStart();
                                }

                                // Prepare payload and try to send reliably (timeout + retry).
                                var payload = new
                                {
                                    players = Players,
                                    deckName = SelectedDeck?.Name,
                                    maxPlayers = MaxPlayers,
                                    timePerRound = TimePerRound,
                                    waitTime = TotalWaitTime
                                };

                                await SendLobbyUpdateWithTimeoutAndRetryAsync(payload);

                                // Always show confirmation to the user after attempting send
                                MessageBox.Show("Room settings updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            MessageBox.Show($"Failed to update room settings: {ex.Message}", "Error");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error opening settings: {ex.Message}", "Error");
            }
        }

        private async Task SendLobbyUpdateWithTimeoutAndRetryAsync(object payload)
        {
            // Try send up to 2 times, each attempt waits up to 2s so UI isn't blocked indefinitely.
            const int timeoutMs = 1500;
            const int maxAttempts = 2;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var sendTask = _realtimeService.SendMessageAsync("LOBBY_UPDATE", payload);
                    var completed = await Task.WhenAny(sendTask, Task.Delay(timeoutMs));
                    if (completed == sendTask)
                    {
                        // ensure any exception from sendTask is observed
                        await sendTask;
                        return;
                    }
                }
                catch
                {
                    // swallow and retry once; do a tiny backoff
                }

                await Task.Delay(150);
            }

            // Fallback: ensure at least local broadcast method is invoked so host UI and other local listeners update
            // BroadcastLobbyState already exists and sends the same message.
            try { BroadcastLobbyState(); } catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (TimeRemaining > 0)
            {
                TimeRemaining--;
                TimeRemainingString = TimeSpan.FromSeconds(TimeRemaining).ToString(@"mm\:ss");
                _realtimeService.SendMessageAsync("TIMER_SYNC", new { seconds = TimeRemaining, isRunning = true });
            }
            else
            {
                _timer.Stop();
                IsTimerRunning = false;
                _realtimeService.SendMessageAsync("START_GAME", new { });
            }
        }

        private PlayerInfo GetMyInfo() => new PlayerInfo { Id = _authService.CurrentUserId ?? Guid.NewGuid().ToString(), Name = CurrentUserName, AvatarUrl = _currentUserAvatar, IsHost = IsHost };
        private async void BroadcastLobbyState() => await _realtimeService.SendMessageAsync("LOBBY_UPDATE", new { players = Players, deckName = SelectedDeck?.Name, maxPlayers = MaxPlayers });

        private void ForceCloseWindow()
        {
            _autoStartTimer?.Stop();
            _heartbeatTimer.Stop();
            _realtimeService.LeaveRoom();
            Application.Current.Dispatcher.Invoke(() => { foreach (Window window in Application.Current.Windows) { if (window.DataContext == this) { window.Close(); break; } } });
        }
    }
}