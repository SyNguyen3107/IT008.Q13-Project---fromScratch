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
    /// ViewModel cơ sở chứa logic chung cho cả Host và Member.
    /// </summary>
    public abstract partial class BaseLobbyViewModel : ObservableObject
    {
        #region Protected Services
        protected readonly IAuthService _authService;
        protected readonly INavigationService _navigationService;
        protected readonly UserSession _userSession;
        protected readonly IClassroomRepository _classroomRepository;
        protected readonly SupabaseService _supabaseService;
        protected readonly AudioService _audioService;
        
        #endregion

        #region Protected Fields
        protected DispatcherTimer _syncTimer;
        protected DispatcherTimer _autoStartTimer;
        protected string _realClassroomIdUUID;
        protected DateTime _lastUpdatedAt = DateTime.MinValue;
        protected HashSet<string> _knownPlayerIds = new HashSet<string>();
        #endregion

        #region Observable Properties (Shared)
        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _currentUserName;
        [ObservableProperty] private bool _isMuted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxPlayersString))]
        private int _maxPlayers = 30;

        public string MaxPlayersString => $"/ {MaxPlayers}";

        [ObservableProperty] private int _timePerRound = 15;
        [ObservableProperty] private int _totalWaitTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private int _autoStartSeconds;
        private bool _showCopyMessage;
        public bool ShowCopyMessage
        {
            get => _showCopyMessage;
            set { _showCopyMessage = value; OnPropertyChanged(); }
        }
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatus))]
        private bool _isAutoStartActive;

        public string AutoStartStatus => $"Starting in: {TimeSpan.FromSeconds(AutoStartSeconds):mm\\:ss}";

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        public bool CanCloseWindow { get; set; } = false;
        #endregion

        public BaseLobbyViewModel(
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService,
            AudioService audioService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _userSession = userSession;
            _classroomRepository = classroomRepository;
            _supabaseService = supabaseService;
            _audioService = audioService;

            CurrentUserName = !string.IsNullOrEmpty(_userSession.UserName) ? _userSession.UserName : "Guest";

            _autoStartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoStartTimer.Tick += AutoStart_Tick;
        }

        /// <summary>
        /// Phương thức khởi tạo async cần được gọi sau khi tạo Instance.
        /// </summary>
        public virtual async Task InitializeAsync(string roomId)
        {
            RoomId = roomId;
            try
            {
                var roomInfo = await _classroomRepository.GetClassroomByCodeAsync(RoomId);
                if (roomInfo == null)
                {
                    MessageBox.Show("Room does not exist!", "Error");
                    ForceCloseWindow();
                    return;
                }

                _realClassroomIdUUID = roomInfo.Id;
                MaxPlayers = roomInfo.MaxPlayers;
                TimePerRound = roomInfo.TimePerRound > 0 ? roomInfo.TimePerRound : 15;
                TotalWaitTime = roomInfo.WaitTime;
                AutoStartSeconds = roomInfo.WaitTime;

                await OnInitializeSpecificAsync(roomInfo);

                await RefreshLobbyState();
                StartPolling();
                _audioService.PlayLoopingAudio("Resources/Sound/Lobby.mp3");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Room initialization error: {ex.Message}", "Error");
                ForceCloseWindow();
            }
        }

        protected abstract Task OnInitializeSpecificAsync(Classroom roomInfo);

        protected abstract Task OnPollingSpecificAsync(List<MemberWithProfile> currentMembers);

        #region Polling Logic
        protected void StartPolling()
        {
            _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _syncTimer.Tick += async (s, e) => await RefreshLobbyState();
            _syncTimer.Start();
        }

        protected void StopPolling()
        {
            _syncTimer?.Stop();
        }

        private async Task RefreshLobbyState()
        {
            try
            {
                var room = await _supabaseService.GetClassroomAsync(_realClassroomIdUUID);
                if (room == null || !room.IsActive)
                {
                    HandleRoomDissolved();
                    return;
                }

                SyncRoomSettings(room);

                var currentMembers = await _supabaseService.GetClassroomMembersWithProfileAsync(_realClassroomIdUUID);

                UpdatePlayerList(currentMembers);

                await OnPollingSpecificAsync(currentMembers);

                if (room.Status == "PLAYING")
                {
                    HandleGameStarted();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling Error: {ex.Message}");
            }
        }

        protected virtual void HandleGameStarted()
        {
            StopPolling();
            NavigateToGame();
        }

        protected void SyncRoomSettings(Classroom room)
        {
            if (MaxPlayers != room.MaxPlayers) MaxPlayers = room.MaxPlayers;
            if (TimePerRound != room.TimePerRound) TimePerRound = room.TimePerRound;

            if (room.UpdatedAt != _lastUpdatedAt)
            {
                _lastUpdatedAt = room.UpdatedAt;
                TotalWaitTime = room.WaitTime;

                var updatedUtc = DateTime.SpecifyKind(room.UpdatedAt, DateTimeKind.Utc);
                var elapsed = (int)(DateTime.Now - updatedUtc).TotalSeconds;
                AutoStartSeconds = Math.Max(room.WaitTime - elapsed, 0);
            }
        }

        private void UpdatePlayerList(List<MemberWithProfile> serverMembers)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentIds = serverMembers.Select(m => m.UserId).ToHashSet();
                var joinedIds = currentIds.Except(_knownPlayerIds).ToList();

                var playersToRemove = Players.Where(p => !currentIds.Contains(p.Id)).ToList();
                foreach (var p in playersToRemove) Players.Remove(p);

                foreach (var member in serverMembers)
                {
                    if (!Players.Any(p => p.Id == member.UserId))
                    {
                        Players.Add(new PlayerInfo
                        {
                            Id = member.UserId,
                            Name = member.DisplayName ?? "Unknown",
                            AvatarUrl = !string.IsNullOrEmpty(member.AvatarUrl) ? member.AvatarUrl : "/Resources/user.png", // Fallback image
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
        #endregion

        #region Common Commands & Methods
        private void AutoStart_Tick(object sender, EventArgs e)
        {
            if (AutoStartSeconds > 0)
            {
                AutoStartSeconds--;
            }
            else
            {
                StopAutoStart();
                OnTimerFinished();
            }
        }

        protected virtual void OnTimerFinished() { } // Để Host override

        protected void StopAutoStart()
        {
            if (_autoStartTimer.IsEnabled) _autoStartTimer.Stop();
            IsAutoStartActive = false;
        }

        protected void HandleRoomDissolved()
        {
            StopPolling();
            MessageBox.Show("The room has been disbanded or no longer exists.", "Notification");

            _navigationService.ShowMainWindow();

            CanCloseWindow = true;
            ForceCloseWindow();
        }

        protected virtual async void NavigateToGame()
        {
            CanCloseWindow = true;

            Deck? deckToPass = GetSelectedDeck(); // Member có thể null

            await _navigationService.ShowMemberGameWindowAsync(
                RoomId,
                _realClassroomIdUUID,
                deckToPass,
                TimePerRound
            );

            ForceCloseWindow();
        }

        protected virtual Deck GetSelectedDeck() => null; // Member trả về null

        [RelayCommand]
        protected void ToggleMute()
        {
            IsMuted = !IsMuted;
            if (IsMuted) _audioService.StopAudio();
            else _audioService.PlayLoopingAudio("Resources/Sound/Lobby.mp3");
        }

        [RelayCommand]
        private async void CopyRoomCode()
        {
            if (!string.IsNullOrEmpty(RoomId))
            {
                Clipboard.SetText(RoomId);
                

                ShowCopyMessage = true;
                await Task.Delay(2000); // Đợi 2 giây
                ShowCopyMessage = false;
            }
        }

        public void ForceCloseWindow()
        {
            _autoStartTimer?.Stop();
            StopPolling();
            _audioService.StopAudio();
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    // Kiểm tra DataContext để đóng đúng cửa sổ
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            });
        }
        #endregion
    }
}