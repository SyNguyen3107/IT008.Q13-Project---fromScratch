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
    /// <summary>
    /// ViewModel quản lý logic của màn hình Sảnh chờ (Lobby).
    /// Chịu trách nhiệm đồng bộ dữ liệu phòng, danh sách người chơi, và xử lý đếm ngược bắt đầu game.
    /// </summary>
    public partial class LobbyViewModel : ObservableObject
    {
        #region Private Services & Fields
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly UserSession _userSession;
        private readonly IDeckRepository _deckRepository;
        private readonly IClassroomRepository _classroomRepository;
        private readonly SupabaseService _supabaseService;
        private readonly AudioService _audioService;
        private DateTime _lastUpdatedAt = DateTime.MinValue;
        private DispatcherTimer _syncTimer;
        private DispatcherTimer _autoStartTimer;
        private string _realClassroomIdUUID;
        private const int HEARTBEAT_TIMEOUT_SECONDS = 15;
        #endregion

        #region Observable Properties
        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _currentUserName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStudent))]
        private bool _isHost = false;

        public bool IsStudent => !IsHost;
        public bool CanCloseWindow { get; set; } = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private int _autoStartSeconds;

        [ObservableProperty] private int _totalWaitTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private bool _isAutoStartActive;

        public string AutoStartStatus => $"Starting in: {TimeSpan.FromSeconds(AutoStartSeconds):mm\\:ss}";

        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty] private Deck _selectedDeck;

        public string MaxPlayersString => $"/ {MaxPlayers}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxPlayersString))]
        private int _maxPlayers = 30;

        [ObservableProperty] private int _timePerRound = 15;

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();
        #endregion

        public LobbyViewModel(
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService,
            AudioService audioService)
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
            _audioService = audioService;
        }

        /// <summary>
        /// Khởi tạo dữ liệu ban đầu cho phòng chờ.
        /// </summary>
        /// <param name="roomId">Mã phòng hiển thị (Room Code).</param>
        /// <param name="isHost">True nếu người dùng là chủ phòng.</param>
        /// <param name="deck">Bộ thẻ bài được chọn (nếu có).</param>
        /// <param name="maxPlayers">Số lượng người chơi tối đa.</param>
        /// <param name="waitTime">Thời gian chờ tự động bắt đầu (giây).</param>
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

                // Cài đặt thông số phòng ban đầu
                MaxPlayers = maxPlayers ?? roomInfo.MaxPlayers;
                TimePerRound = roomInfo.TimePerRound > 0 ? roomInfo.TimePerRound : 15;
                TotalWaitTime = roomInfo.WaitTime;
                AutoStartSeconds = roomInfo.WaitTime;

                if (IsHost)
                {
                    await InitializeHostAsync(deck, waitTime);
                }
                else
                {
                    await InitializeStudentAsync(roomInfo, waitTime);
                }

                // Tải dữ liệu lần đầu và bắt đầu Polling
                await RefreshLobbyState();
                StartPolling();
                _audioService.PlayLoopingAudio("Resources/Sound/Lobby.mp3");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo phòng: {ex.Message}");
                ForceCloseWindow();
            }
        }

        /// <summary>
        /// Logic khởi tạo dành riêng cho Host (Load bộ bài, setup timer).
        /// </summary>
        private async Task InitializeHostAsync(Deck deck, int? waitTime)
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

        /// <summary>
        /// Logic khởi tạo dành riêng cho Student (Join phòng, đồng bộ thời gian chờ).
        /// </summary>
        private async Task InitializeStudentAsync(Classroom roomInfo, int? waitTime)
        {
            var myId = _authService.CurrentUserId ?? _userSession.UserId;
            await _supabaseService.AddMemberAsync(_realClassroomIdUUID, myId);

            // Tính toán thời gian trôi qua để đồng bộ Timer
            var updatedUtc = DateTime.SpecifyKind(roomInfo.UpdatedAt, DateTimeKind.Utc);
            var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;

            _lastUpdatedAt = roomInfo.UpdatedAt;
            AutoStartSeconds = Math.Max(roomInfo.WaitTime - elapsed, 0);

            if (waitTime.HasValue && waitTime.Value > 0)
            {
                IsAutoStartActive = true;
                _autoStartTimer.Start();
            }
            else
            {
                MessageBox.Show("⏰ Hết giờ, bạn không thể join vào phòng này nữa.", "Thông báo");
                AutoStartSeconds = 0;
                IsAutoStartActive = false;
                CanCloseWindow = true;
                ForceCloseWindow();
            }
            _audioService.PlayOneShot("Resources/Sound/PlayerJoin.mp3");
        }

        /// <summary>
        /// Bắt đầu vòng lặp cập nhật trạng thái phòng (3 giây/lần).
        /// </summary>
        private void StartPolling()
        {
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromSeconds(3);
            _syncTimer.Tick += async (s, e) => await RefreshLobbyState();
            _syncTimer.Start();
        }

        /// <summary>
        /// Dừng vòng lặp cập nhật.
        /// </summary>
        private void StopPolling()
        {
            _syncTimer?.Stop();
        }

        /// <summary>
        /// Hàm cốt lõi để đồng bộ dữ liệu:
        /// 1. Kiểm tra trạng thái phòng.
        /// 2. Gửi Heartbeat (nếu là Member).
        /// 3. Cập nhật danh sách thành viên.
        /// 4. Kick thành viên AFK (nếu là Host).
        /// 5. Kiểm tra trạng thái bắt đầu game.
        /// </summary>
        private async Task RefreshLobbyState()
        {
            try
            {
                // 1. Kiểm tra phòng tồn tại
                var room = await _supabaseService.GetClassroomAsync(_realClassroomIdUUID);
                if (room == null || !room.IsActive)
                {
                    HandleRoomDissolved();
                    return;
                }

                // 2. Đồng bộ Settings
                SyncRoomSettings(room);

                var myId = _authService.CurrentUserId ?? _userSession.UserId;

                // 3. Gửi Heartbeat (Member only)
                if (!IsHost)
                {
                    _ = _supabaseService.SendHeartbeatAsync(_realClassroomIdUUID, myId);
                }

                // 4. Lấy danh sách thành viên mới nhất
                var currentMembers = await _supabaseService.GetClassroomMembersWithProfileAsync(_realClassroomIdUUID);

                // 5. Host xử lý kick người dùng mất kết nối
                if (IsHost)
                {
                    await HandleAutoKickAsync(currentMembers, myId);
                }

                // 6. Cập nhật UI
                UpdatePlayerList(currentMembers);

                // 7. Kiểm tra trạng thái Game Start
                if (room.Status == "PLAYING" && !IsHost)
                {
                    NavigateToGame();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Cập nhật các thiết lập phòng nếu có thay đổi từ server.
        /// </summary>
        private void SyncRoomSettings(Classroom room)
        {
            if (MaxPlayers != room.MaxPlayers) MaxPlayers = room.MaxPlayers;
            if (TimePerRound != room.TimePerRound) TimePerRound = room.TimePerRound;

            if (room.UpdatedAt != _lastUpdatedAt && !IsHost)
            {
                _lastUpdatedAt = room.UpdatedAt;
                TotalWaitTime = room.WaitTime;
                var updatedUtc = DateTime.SpecifyKind(room.UpdatedAt, DateTimeKind.Utc);
                var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;
                AutoStartSeconds = Math.Max(room.WaitTime - elapsed, 0);
            }
        }

        /// <summary>
        /// Xử lý khi phòng bị giải tán.
        /// </summary>
        private void HandleRoomDissolved()
        {
            StopPolling();
            MessageBox.Show("Chủ phòng đã giải tán phòng chơi.", "Thông báo");
            CanCloseWindow = true;
            ForceCloseWindow();
        }

        /// <summary>
        /// Logic Host tự động kick thành viên không phản hồi quá thời gian quy định.
        /// Sử dụng so sánh giờ UTC chuẩn.
        /// </summary>
        private async Task HandleAutoKickAsync(List<MemberWithProfile> currentMembers, string myId)
            {
            var now = DateTime.Now;
            var usersToKick = new List<string>();
            
            foreach (var member in currentMembers)
            {
                if (member.UserId == myId) continue;

                if (member.LastActive != DateTime.Now)
                {
                    DateTime lastActiveUtc;

                    // Chuẩn hóa thời gian về UTC để so sánh chính xác
                    if (member.LastActive.Kind == DateTimeKind.Unspecified)
                    {
                        lastActiveUtc = DateTime.SpecifyKind(member.LastActive, DateTimeKind.Utc);
                    }
                    else
                    {
                        lastActiveUtc = member.LastActive.ToUniversalTime();
                    }

                    if ((now - lastActiveUtc).TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
                    {
                        usersToKick.Add(member.UserId);
                    }
                }
            }
                    
            // Thực hiện xóa user khỏi DB và list tạm
            foreach (var userId in usersToKick)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoKick] Kicking user: {userId}");
                await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, userId);

                var memberToRemove = currentMembers.FirstOrDefault(x => x.UserId == userId);
                if (memberToRemove != null)
                {
                    currentMembers.Remove(memberToRemove);
                }
            }
        }

        private HashSet<string> _knownPlayerIds = new HashSet<string>();
        /// <summary>
        /// Đồng bộ danh sách hiển thị trên UI với dữ liệu từ Server.
        /// Đảm bảo thread-safety khi thao tác với ObservableCollection.
        /// </summary>
        /// <param name="serverMembers">Danh sách thành viên lấy từ server.</param>
        private void UpdatePlayerList(List<MemberWithProfile> serverMembers)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentIds = serverMembers.Select(m => m.UserId).ToHashSet();

                // Xóa người không còn trong phòng
                var idsFromServer = serverMembers.Select(x => x.UserId).ToHashSet();
                var playersToRemove = Players.Where(p => !idsFromServer.Contains(p.Id)).ToList();
                foreach (var p in playersToRemove) Players.Remove(p);

                var joinedIds = currentIds.Except(_knownPlayerIds).ToList();
                // Thêm người mới vào phòng
                foreach (var member in serverMembers)
                {
                    if (!Players.Any(p => p.Id == member.UserId))
                    {
                        Players.Add(new PlayerInfo
                        {
                            Id = member.UserId,
                            Name = member.DisplayName ?? "Unknown",
                            AvatarUrl = !string.IsNullOrEmpty(member.AvatarUrl) ? member.AvatarUrl : "/Images/default_user.png",
                            IsHost = (member.Role == "owner" || member.Role == "host")
                        });
                       
                    }
                }
                _knownPlayerIds = currentIds;

                if (joinedIds.Any())
                {
                    _audioService.PlayOneShot("Resources/Sound/PlayerJoin.mp3");
                }

            });
        }

        /// <summary>
        /// Chuyển hướng sang màn hình Game khi nhận tín hiệu bắt đầu.
        /// </summary>
        private void NavigateToGame()
        {
            CanCloseWindow = true;
            _navigationService.ShowGameWindowAsync(
                  RoomId,
                  _realClassroomIdUUID,
                  SelectedDeck,
                  MaxPlayers,
                  TimePerRound
                );
            ForceCloseWindow();
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
                    StartGameCommand.Execute(null);
                }
            }
        }

        private void StopAutoStart()
        {
            if (_autoStartTimer.IsEnabled) _autoStartTimer.Stop();
            IsAutoStartActive = false;
        }

        /// <summary>
        /// Lệnh rời phòng (dành cho Member).
        /// </summary>
        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Rời phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    StopPolling();
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

        /// <summary>
        /// Lệnh giải tán phòng (dành cho Host).
        /// </summary>
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

        /// <summary>
        /// Lệnh bắt đầu game ngay lập tức.
        /// </summary>
        [RelayCommand]
        private async Task StartGame()
        {
            try
            {
                StopAutoStart();
                StopPolling();

                await _classroomRepository.UpdateStatusAsync(_realClassroomIdUUID, "PLAYING");
                NavigateToGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting game: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Mở cửa sổ cài đặt phòng (chỉ Host).
        /// </summary>
        [RelayCommand]
        private async Task OpenSettings()
        {
            if (!IsHost) return;

            try
            {
                var settingsVm = new SettingsViewModel(_deckRepository, SelectedDeck, MaxPlayers, TimePerRound, TotalWaitTime);
                var settingsWindow = new Views.SettingsWindow(settingsVm);

                if (settingsWindow.ShowDialog() == true)
                {
                    // Cập nhật cài đặt lên DB
                    await _classroomRepository.UpdateClassroomSettingsAsync(
                        _realClassroomIdUUID,
                        settingsVm.SelectedDeck?.Id,
                        settingsVm.MaxPlayers,
                        settingsVm.TimePerRound,
                        settingsVm.WaitTimeMinutes * 60
                    );

                    // Cập nhật Local state
                    TotalWaitTime = settingsVm.WaitTimeMinutes * 60;
                    AutoStartSeconds = TotalWaitTime;
                    MaxPlayers = settingsVm.MaxPlayers;

                    // Restart Auto-start timer
                    IsAutoStartActive = true;
                    _autoStartTimer.Start();

                    MessageBox.Show("Cập nhật cài đặt phòng thành công.", "Thành công");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở cài đặt: {ex.Message}", "Lỗi");
            }
        }

        /// <summary>
        /// Đóng cửa sổ hiện tại an toàn, đảm bảo ngắt mọi kết nối nền.
        /// </summary>

     
        [ObservableProperty]
        private bool _isMuted;

        [RelayCommand]
        private void ToggleMute()
        {
            IsMuted = !IsMuted;

            if (IsMuted)
            {
                _audioService.StopAudio(); // hoặc giảm volume về 0
            }
            else
            {
                _audioService.PlayLoopingAudio("Resources/Sound/Lobby.mp3"); // bật lại nhạc
            }
        }

        [RelayCommand]
        private void CopyRoomCode()
        {
            if (!string.IsNullOrEmpty(RoomId))
            {
                Clipboard.SetText(RoomId);
                MessageBox.Show("Room Code đã được copy vào clipboard!");
            }
        }

        private void ForceCloseWindow()
        {
            _autoStartTimer?.Stop();
            StopPolling();
            _audioService.StopAudio();
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