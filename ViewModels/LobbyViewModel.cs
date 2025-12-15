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
        // Services
        private readonly RealtimeService _realtimeService;
        private readonly IAuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly IClassroomRepository _classroomRepository;
        private readonly IDeckRepository _deckRepository;
        private readonly UserSession _userSession;

        // Properties
        [ObservableProperty] private string _roomId;
        [ObservableProperty] private bool _isHost;
        public bool IsStudent => !IsHost;
        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        // Timer Ping
        private DispatcherTimer _pingTimer;

        // Settings
        [ObservableProperty] private int _maxPlayers = 30;
        [ObservableProperty] private int _timePerRound = 15;
        [ObservableProperty] private int _totalWaitTime = 0;
        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();
        [ObservableProperty] private Deck _selectedDeck;

        // Thêm các biến còn thiếu để tránh lỗi biên dịch
        [ObservableProperty] private string _currentUserName;
        [ObservableProperty] private string _currentUserAvatar;

        public bool CanCloseWindow { get; set; } = false;

        public LobbyViewModel(
            RealtimeService realtimeService,
            IAuthService authService,
            SupabaseService supabaseService,
            IClassroomRepository classroomRepository,
            IDeckRepository deckRepository,
            UserSession userSession)
        {
            _realtimeService = realtimeService;
            _authService = authService;
            _supabaseService = supabaseService;
            _classroomRepository = classroomRepository;
            _deckRepository = deckRepository;
            _userSession = userSession;

            // Khởi tạo thông tin User
            CurrentUserName = !string.IsNullOrEmpty(_userSession.UserName) ? _userSession.UserName : "Guest";
            CurrentUserAvatar = "ava1";

            // Setup Realtime
            if (_supabaseService.Client != null) _realtimeService.SetClient(_supabaseService.Client);

            // [FIX] Đồng bộ tên sự kiện: OnMessageReceived
            _realtimeService.OnMessageReceived += OnRealtimeSignal;

            // Timer Ping
            _pingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _pingTimer.Tick += (s, e) => {
                if (!IsHost) SendMyInfo("JOIN");
            };
        }

        public async Task InitializeAsync(string roomId, bool isHost, Deck deck = null, int? maxPlayers = null, int? waitTime = null)
        {
            RoomId = roomId;
            IsHost = isHost;
            Players.Clear();

            Players.Add(GetMyInfo());

            if (IsHost)
            {
                try
                {
                    var decks = await _deckRepository.GetAllAsync();
                    foreach (var d in decks) AvailableDecks.Add(d);
                    SelectedDeck = deck ?? decks.FirstOrDefault();
                }
                catch { }
            }
            else
            {
                try
                {
                    var room = await _classroomRepository.GetClassroomByCodeAsync(RoomId);
                    if (room != null) await _supabaseService.AddMemberAsync(room.Id, GetMyInfo().Id);
                }
                catch { }
            }

            await ConnectRealtime();
        }

        private async Task ConnectRealtime()
        {
            try
            {
                await _realtimeService.ConnectAsync();
                // [FIX] Đồng bộ tên hàm: JoinRoomAsync
                await _realtimeService.JoinRoomAsync(RoomId);

                if (!IsHost)
                {
                    SendMyInfo("JOIN");
                    _pingTimer.Start();
                }
            }
            catch { }
        }

        // --- XỬ LÝ TÍN HIỆU TỪ SERVER ---
        private void OnRealtimeSignal(string action, JObject payload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    switch (action)
                    {
                        case "JOIN":
                            if (IsHost) HandleClientJoin(payload);
                            break;

                        case "LEFT":
                            if (IsHost) HandleClientLeft(payload);
                            break;

                        case "SYNC_LOBBY":
                            if (!IsHost) HandleSyncLobby(payload);
                            break;

                        case "CLOSE":
                            if (!IsHost) ForceQuit("Phòng đã bị giải tán.");
                            break;
                    }
                }
                catch { }
            });
        }

        // --- LOGIC CỦA HOST ---
        private void HandleClientJoin(JObject payload)
        {
            var p = payload.ToObject<PlayerInfo>();
            if (p == null) return;

            if (!Players.Any(x => x.Id == p.Id))
            {
                Players.Add(p);
                BroadcastState();
            }
        }

        private void HandleClientLeft(JObject payload)
        {
            string id = payload["Id"]?.ToString();
            var p = Players.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                Players.Remove(p);
                BroadcastState();
            }
        }

        private void BroadcastState()
        {
            var state = new
            {
                Players = Players,
                Max = MaxPlayers,
                Time = TimePerRound
            };
            // [FIX] Đồng bộ tên hàm: SendMessageAsync
            _realtimeService.SendMessageAsync("SYNC_LOBBY", state);
        }

        // --- LOGIC CỦA CLIENT ---
        private void HandleSyncLobby(JObject payload)
        {
            var serverPlayers = payload["Players"]?.ToObject<List<PlayerInfo>>();
            if (serverPlayers == null) return;

            var myId = GetMyInfo().Id;

            // 1. Thêm người mới
            foreach (var sp in serverPlayers)
            {
                if (!Players.Any(local => local.Id == sp.Id)) Players.Add(sp);
            }

            // 2. Xóa người cũ (TRỪ CHÍNH MÌNH)
            var toRemove = Players.Where(local => local.Id != myId && !serverPlayers.Any(sp => sp.Id == local.Id)).ToList();
            foreach (var r in toRemove) Players.Remove(r);

            // 3. Sync Settings
            if (payload["Max"] != null) MaxPlayers = payload["Max"].Value<int>();
            if (payload["Time"] != null) TimePerRound = payload["Time"].Value<int>();
        }

        // --- CÁC HÀM NGƯỜI DÙNG ---
        [RelayCommand]
        private void LeaveRoom()
        {
            if (MessageBox.Show("Rời phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // [FIX] Đồng bộ tên hàm: SendMessageAsync
                _realtimeService.SendMessageAsync("LEFT", new { Id = GetMyInfo().Id });
                ForceQuit();
            }
        }

        [RelayCommand]
        private void CloseRoom()
        {
            if (MessageBox.Show("Giải tán?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _realtimeService.SendMessageAsync("CLOSE", new { });
                ForceQuit();
            }
        }

        // [FIX] Hàm ForceQuit an toàn, sửa lỗi Crash và lỗi CS1023
        private void ForceQuit(string msg = null)
        {
            CanCloseWindow = true;
            _pingTimer?.Stop();
            if (msg != null) MessageBox.Show(msg);

            Application.Current.Dispatcher.Invoke(() =>
            {
                var list = new List<Window>();
                foreach (Window w in Application.Current.Windows) list.Add(w);
                foreach (var w in list) { if (w.DataContext == this) { w.Close(); break; } }
            });

            // Dọn dẹp ngầm
            Task.Run(async () => {
                // [FIX] Check null an toàn và dùng đúng tên hàm LeaveRoom
                if (_realtimeService != null) await _realtimeService.LeaveRoom();

                try
                {
                    var room = await _classroomRepository.GetClassroomByCodeAsync(RoomId);
                    if (room != null)
                    {
                        if (IsHost)
                            await _classroomRepository.DeleteClassroomAsync(RoomId);
                        else
                            await _classroomRepository.RemoveMemberAsync(room.Id, GetMyInfo().Id);
                    }
                }
                catch { }
            });
        }

        private PlayerInfo GetMyInfo() => new PlayerInfo
        {
            Id = _authService.CurrentUserId ?? _userSession.UserId,
            Name = CurrentUserName,
            AvatarUrl = _currentUserAvatar,
            IsHost = IsHost
        };

        // [FIX] Đồng bộ tên hàm: SendMessageAsync
        private void SendMyInfo(string action) => _realtimeService.SendMessageAsync(action, GetMyInfo());
    }
}