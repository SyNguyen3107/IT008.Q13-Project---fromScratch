using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Repositories;
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
        private readonly SupabaseService _supabaseService;

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

        public string MaxPlayersString => $"/ {MaxPlayers}";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxPlayersString))]
        private int _maxPlayers = 30;

        [ObservableProperty] private int _timePerRound = 15;

        private DispatcherTimer _autoStartTimer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private int _autoStartSeconds;

        [ObservableProperty]
        private int _totalWaitTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private bool _isAutoStartActive;

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
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService)
        {
            _realtimeService = realtimeService;
        _auth_service: _realtimeService = realtimeService; // <- keep original field assignment consistent
            _realtimeService = realtimeService;
            _authService = authService;
            _navigationService = navigationService;
            _userSession = userSession;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;
            _supabaseService = supabaseService;

            CurrentUserName = !string.IsNullOrEmpty(_userSession.UserName) ? _userSession.UserName : "Guest";

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _heartbeatTimer.Tick += Heartbeat_Tick;

            _realtimeService.OnMessageReceived += HandleRealtimeMessage;

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

                if (maxPlayers.HasValue) MaxPlayers = maxPlayers.Value;
                else if (roomInfo.MaxPlayers > 0) MaxPlayers = roomInfo.MaxPlayers;

                if (roomInfo.TimePerRound > 0) TimePerRound = roomInfo.TimePerRound;

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
                    var classroom = await _supabaseService.GetClassroomAsync(_realClassroomIdUUID);
                    if (classroom != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (classroom.MaxPlayers > 0) MaxPlayers = classroom.MaxPlayers;
                            if (classroom.TimePerRound > 0) TimePerRound = classroom.TimePerRound;
                            TotalWaitTime = classroom.WaitTime;
                            AutoStartSeconds = classroom.WaitTime;
                        });
                    }
                    await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);
                }
                


                if (isHost && waitTime.HasValue && waitTime.Value > 0)
                {
                    TotalWaitTime = waitTime.Value;
                    AutoStartSeconds = waitTime.Value;
                    IsAutoStartActive = true;
                    _autoStartTimer.Start();
                }

                await ConnectAndJoinRoom();
                _heartbeatTimer.Start();

                // 🔑 Subscribe classroom updates
                await _supabaseService.SubscribeToClassroomAsync(_realClassroomIdUUID, classroom =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (classroom.MaxPlayers > 0) MaxPlayers = classroom.MaxPlayers;
                        if (classroom.TimePerRound > 0) TimePerRound = classroom.TimePerRound;
                        TotalWaitTime = classroom.WaitTime;
                        AutoStartSeconds = classroom.WaitTime;
                    });
                });

                // 🔑 Subscribe members events
                await _supabaseService.SubscribeToClassroomMembersAllEventsAsync(
                    _realClassroomIdUUID,
                    onInsert: member =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (!Players.Any(p => p.Id == member.UserId))
                            {
                                Players.Add(new PlayerInfo
                                {
                                    Id = member.UserId,
                                    Name = $"Player {member.UserId.Substring(Math.Max(0, member.UserId.Length - 4))}",
                                    IsHost = member.Role == "owner"
                                });
                            }
                        });
                    },
                    onDelete: member =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var existing = Players.FirstOrDefault(p => p.Id == member.UserId);
                            if (existing != null) Players.Remove(existing);
                        });
                    }
                );
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
                var members = await _supabaseService.GetClassroomMembersWithProfileAsync(_realClassroomIdUUID);
                Players.Clear();
                foreach (var m in members)
                {
                    Players.Add(new PlayerInfo
                    {
                        Id = m.UserId,
                        Name = m.DisplayName,
                        AvatarUrl = m.AvatarUrl,
                        IsHost = m.Role == "owner"
                    });
                    _lastSeenMap[m.UserId] = DateTime.Now;
                }

                var myInfo = GetMyInfo();
                await _supabaseService.JoinRoomPresenceAsync(
                        _realClassroomIdUUID,
                        myInfo.Id,
                        myInfo.Name, // hoặc CurrentUserName
                        presenceList => {
                            // Callback khi có danh sách người đang online
                            // Ví dụ: cập nhật UI hoặc log ra console
                            DebugLogs.Add($"Presence sync: {string.Join(", ", presenceList)}");
    });

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connect error: {ex.Message}");
            }
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

                    if (!string.IsNullOrEmpty(_realClassroomIdUUID) && _supabaseService != null)
                    {
                        _ = _supabaseService.UnsubscribeFromClassroomAsync(_realClassroomIdUUID);
                        _ = _supabaseService.LeaveRoomPresenceAsync(_realClassroomIdUUID, _authService.CurrentUserId ?? _userSession.UserId);
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

        private void AutoStart_Tick(object sender, EventArgs e)
        {
            if (AutoStartSeconds > 0)
            {
                AutoStartSeconds--;
            }
            else
            {
                StopAutoStart();
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
                CanCloseWindow = true;

                try
                {
                    if (IsHost)
                    {
                        await _classroomRepository.DeleteClassroomAsync(RoomId);
                    }
                }
                catch (Exception) { }

                try
                {
                    var sendTask = _realtimeService.SendMessageAsync("CLOSE_ROOM", new { });
                    var timeoutTask = Task.Delay(2000);
                    await Task.WhenAny(sendTask, timeoutTask);
                }
                catch { }

                if (!string.IsNullOrEmpty(_realClassroomIdUUID) && _supabaseService != null)
                {
                    _ = _supabaseService.UnsubscribeFromClassroomAsync(_realClassroomIdUUID);
                    _ = _supabaseService.LeaveRoomPresenceAsync(_realClassroomIdUUID, _authService.CurrentUserId ?? _userSession.UserId);
                }

                ForceCloseWindow();
            }
        }

        [RelayCommand]
        private void StartGameCommand()
        {
            StopAutoStart();

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

                                var payload = new
                                {
                                    players = Players,
                                    deckName = SelectedDeck?.Name,
                                    maxPlayers = MaxPlayers,
                                    timePerRound = TimePerRound,
                                    waitTime = TotalWaitTime
                                };

                                await SendLobbyUpdateWithTimeoutAndRetryAsync(payload);

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
                        await sendTask;
                        return;
                    }
                }
                catch { }

                await Task.Delay(150);
            }

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

            if (!string.IsNullOrEmpty(_realClassroomIdUUID) && _supabaseService != null)
            {
                _ = _supabaseService.UnsubscribeFromClassroomAsync(_realClassroomIdUUID);
                _ = _supabaseService.LeaveRoomPresenceAsync(_realClassroomIdUUID, _authService.CurrentUserId ?? _userSession.UserId);
            }

            Application.Current.Dispatcher.Invoke(() => { foreach (Window window in Application.Current.Windows) { if (window.DataContext == this) { window.Close(); break; } } });
        }
    }
}