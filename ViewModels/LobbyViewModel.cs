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

        [ObservableProperty] private string _roomId; // Đây là CODE (ví dụ RN63Q3)
        private string _realClassroomIdUUID;         // Đây là UUID thật trong DB

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
        [ObservableProperty] private int _maxPlayers = 30;

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        private Dictionary<string, DateTime> _lastSeenMap = new Dictionary<string, DateTime>();
        private DispatcherTimer _heartbeatTimer;
        public ObservableCollection<string> DebugLogs { get; } = new ObservableCollection<string>();

        private void AddLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                DebugLogs.Insert(0, $"[{time}] {message}");
                if (DebugLogs.Count > 50) DebugLogs.RemoveAt(DebugLogs.Count - 1);
            });
        }

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
        }

        public async Task InitializeAsync(string roomId, bool isHost, Deck deck = null)
        {
            RoomId = roomId;
            IsHost = isHost;
            CanCloseWindow = false;

            AddLog($"Init Step 1: Resolving Room Code '{RoomId}'...");
            try
            {
                // 1. Lấy thông tin phòng để có UUID
                var roomInfo = await _classroomRepository.GetClassroomByCodeAsync(RoomId);

                if (roomInfo == null)
                {
                    MessageBox.Show("Phòng không tồn tại!");
                    ForceCloseWindow();
                    return;
                }

                _realClassroomIdUUID = roomInfo.Id; // [QUAN TRỌNG] Lưu UUID
                AddLog($"-> Resolved UUID: {_realClassroomIdUUID}");

                // 2. Nếu là Host, load Deck
                if (IsHost)
                {
                    var decks = await _deckRepository.GetAllAsync();
                    AvailableDecks.Clear();
                    foreach (var d in decks) AvailableDecks.Add(d);
                    SelectedDeck = deck ?? decks.FirstOrDefault();
                }
                else
                {
                    // 3. Nếu là Student, tự ghi tên vào DB
                    AddLog("-> Registering to DB members list...");
                    var myId = _authService.CurrentUserId ?? _userSession.UserId;

                    // Gọi hàm AddMemberAsync (đã thêm vào Repo)
                    await _classroomRepository.AddMemberAsync(_realClassroomIdUUID, myId);
                }

                // 4. Kết nối và tải danh sách
                await ConnectAndJoinRoom();

                // 5. Bắt đầu Heartbeat
                _heartbeatTimer.Start();
                AddLog("Heartbeat Timer Started");
            }
            catch (Exception ex)
            {
                AddLog($"CRITICAL INIT ERROR: {ex.Message}");
            }
        }

        private async Task ConnectAndJoinRoom()
        {
            try
            {
                // --- BƯỚC 1: Lấy danh sách thành viên từ DB ---
                // [FIX BUG]: Dùng _realClassroomIdUUID (UUID) thay vì RoomId (Code)
                // Và bỏ if (IsHost) để Student cũng thấy danh sách

                AddLog("Step 2: Fetching members from DB...");
                var currentMembers = await _classroomRepository.GetMembersAsync(_realClassroomIdUUID);

                Players.Clear();
                foreach (var m in currentMembers)
                {
                    Players.Add(new PlayerInfo
                    {
                        Id = m.UserId,
                        Name = m.Profile?.DisplayName ?? "Unknown",
                        AvatarUrl = m.Profile?.AvatarUrl,
                        IsHost = m.Role == "owner"
                    });
                    _lastSeenMap[m.UserId] = DateTime.Now;
                }
                AddLog($"-> Found {currentMembers.Count} members in DB.");

                // --- BƯỚC 2: Kết nối Socket ---
                AddLog("Step 3: Connecting to Realtime...");

                var connectTask = _realtimeService.ConnectAsync();
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    AddLog("ERROR: Connect Timeout!");
                    return; // Timeout thì thôi, nhưng danh sách DB đã load được rồi
                }
                await connectTask;
                AddLog("-> Socket Connected!");

                // --- BƯỚC 3: Vào phòng ---
                // Topic dùng RoomId (Code) vẫn OK
                AddLog($"Step 4: Joining Channel 'room:{RoomId}'...");

                var joinTask = _realtimeService.JoinRoomAsync(RoomId);
                if (await Task.WhenAny(joinTask, Task.Delay(5000)) != joinTask)
                {
                    AddLog("ERROR: Join Room Timeout!");
                    return;
                }
                await joinTask;
                AddLog("-> Joined Room Successfully!");

                // --- BƯỚC 4: Gửi tin nhắn chào hỏi ---
                var myInfo = GetMyInfo();
                await _realtimeService.SendMessageAsync("PLAYER_JOIN", myInfo);
            }
            catch (Exception ex)
            {
                AddLog($"CONNECT ERROR: {ex.Message}");
            }
        }

        // --- CÁC HÀM XỬ LÝ KHÁC (Giữ nguyên) ---

        private void HandleRealtimeMessage(string eventName, object data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // AddLog($"RX: {eventName}"); // Tạm tắt cho đỡ rối mắt
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

        // [SỬA]: LeaveRoom cần xóa tên khỏi DB
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

        // ... (Các hàm HandleHeartbeat, Heartbeat_Tick, CheckForOfflineUsers... giữ nguyên như cũ) ...

        private void HandleHeartbeat(JObject json)
        {
            if (!IsHost) return;
            string userId = json["id"]?.ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                if (!Players.Any(p => p.Id == userId))
                {
                    // Có thể gọi DB load lại nếu thấy user lạ
                }
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

        // Các hàm xử lý khác (Kick, Close, Timer...) giữ nguyên code cũ
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
                // _navigationService.ShowGameWindow...
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
                try { if (IsHost) await _classroomRepository.DeleteClassroomAsync(RoomId); } catch { }
                await _realtimeService.SendMessageAsync("CLOSE_ROOM", new { });
                CanCloseWindow = true;
                ForceCloseWindow();
            }
        }

        [RelayCommand]
        private void StartGameCommand()
        {
            TimeRemaining = 5; IsTimerRunning = true; _timer.Start();
            _realtimeService.SendMessageAsync("TIMER_SYNC", new { seconds = TimeRemaining, isRunning = true });
        }

        [RelayCommand] private void OpenSettings() => MessageBox.Show($"Deck: {SelectedDeck?.Name}");

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (TimeRemaining > 0) { TimeRemaining--; TimeRemainingString = TimeSpan.FromSeconds(TimeRemaining).ToString(@"mm\:ss"); _realtimeService.SendMessageAsync("TIMER_SYNC", new { seconds = TimeRemaining, isRunning = true }); }
            else { _timer.Stop(); IsTimerRunning = false; _realtimeService.SendMessageAsync("START_GAME", new { }); }
        }

        private PlayerInfo GetMyInfo() => new PlayerInfo { Id = _authService.CurrentUserId ?? Guid.NewGuid().ToString(), Name = CurrentUserName, AvatarUrl = _currentUserAvatar, IsHost = IsHost };
        private async void BroadcastLobbyState() => await _realtimeService.SendMessageAsync("LOBBY_UPDATE", new { players = Players, deckName = SelectedDeck?.Name, maxPlayers = MaxPlayers });

        private void ForceCloseWindow()
        {
            _heartbeatTimer.Stop();
            _realtimeService.LeaveRoom();
            Application.Current.Dispatcher.Invoke(() => { foreach (Window window in Application.Current.Windows) { if (window.DataContext == this) { window.Close(); break; } } });
        }
    }
}