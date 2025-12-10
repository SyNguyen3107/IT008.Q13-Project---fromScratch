using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class LobbyViewModel : ObservableObject
    {
        private readonly RealtimeService _realtimeService;
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly UserSession _userSession; // Cần để lấy Email làm tên tạm

        // --- Properties ---
        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _currentUserName;

        // Trạng thái giao diện
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotInLobby))]
        private bool _isInLobby = false; // False: Hiện menu nhập mã, True: Hiện danh sách

        public bool IsNotInLobby => !IsInLobby;

        [ObservableProperty] private bool _isHost = false;    // True: Hiện nút Bắt đầu

        // Danh sách hiển thị lên UI
        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        public LobbyViewModel(RealtimeService realtimeService, IAuthService authService, INavigationService navigationService, UserSession userSession)
        {
            _realtimeService = realtimeService;
            _authService = authService;
            _navigationService = navigationService;
            _userSession = userSession;

            // Tạm thời lấy Email hoặc ID cắt ngắn làm tên hiển thị
            // Sau này Database xong sẽ thay bằng _supabaseService.GetProfile().DisplayName
            string email = _userSession.Email ?? "User";
            CurrentUserName = email.Contains("@") ? email.Split('@')[0] : email;

            // Đăng ký nhận tin nhắn
            _realtimeService.OnMessageReceived += HandleRealtimeMessage;
        }

        // --- XỬ LÝ TIN NHẮN REALTIME (Core Logic) ---
        private void HandleRealtimeMessage(string eventName, object data)
        {
            // Buộc phải chạy trên UI Thread vì thao tác với ObservableCollection
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Convert data sang JObject để dễ xử lý
                    var json = JObject.FromObject(data);

                    switch (eventName)
                    {
                        case "PLAYER_JOIN":
                            HandlePlayerJoin(json);
                            break;

                        case "LOBBY_UPDATE":
                            HandleLobbyUpdate(json);
                            break;

                        case "START_GAME":
                            // Logic chuyển màn hình chơi game (sẽ làm ở task sau)
                            MessageBox.Show("Giáo viên đã bắt đầu bài học!", "Thông báo");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
                }
            });
        }

        // Host xử lý khi có người mới vào
        private void HandlePlayerJoin(JObject json)
        {
            // Chỉ Host mới có quyền quản lý danh sách
            if (!IsHost) return;

            var newPlayer = json.ToObject<PlayerInfo>();
            if (newPlayer == null) return;

            // Kiểm tra trùng lặp (nếu mạng lag gửi 2 lần)
            if (!Players.Any(p => p.Id == newPlayer.Id))
            {
                // Task: Logic Check Full Slot
                if (Players.Count >= 30) // Giới hạn 30 người
                {
                    // Có thể gửi tin nhắn "ROOM_FULL" lại cho người đó (Nâng cao)
                    return;
                }

                Players.Add(newPlayer);

                // Gửi danh sách mới nhất cho tất cả mọi người (Sync)
                BroadcastLobbyState();
            }
        }

        // Client xử lý khi nhận danh sách mới từ Host
        private void HandleLobbyUpdate(JObject json)
        {
            // Lấy mảng "players" từ gói tin
            var playersList = json["players"]?.ToObject<List<PlayerInfo>>();

            if (playersList != null)
            {
                Players.Clear();
                foreach (var p in playersList)
                {
                    Players.Add(p);
                }
            }
        }

        // --- COMMANDS (Nút bấm) ---

        // Task: Logic Tạo phòng
        [RelayCommand]
        private async Task CreateRoom()
        {
            try
            {
                // 1. Sinh mã phòng (6 ký tự chữ hoa)
                var random = new Random();
                const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Bỏ I, O, 1, 0 tránh nhầm lẫn
                RoomId = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());

                IsHost = true;
                IsInLobby = true;
                Players.Clear();

                // 2. Thêm chính mình (Host) vào danh sách
                var myInfo = GetMyInfo();
                myInfo.IsHost = true;
                myInfo.Name += " (Host)";
                Players.Add(myInfo);

                // 3. Kết nối Realtime
                await _realtimeService.ConnectAsync();
                await _realtimeService.JoinRoomAsync(RoomId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo phòng: {ex.Message}");
                IsInLobby = false;
            }
        }

        // Task: Logic Join phòng & Gửi USER_JOINED
        [RelayCommand]
        private async Task JoinRoom()
        {
            // Validation cơ bản
            if (string.IsNullOrWhiteSpace(RoomId) || RoomId.Length < 4)
            {
                MessageBox.Show("Vui lòng nhập mã phòng hợp lệ.");
                return;
            }

            try
            {
                IsHost = false;
                IsInLobby = true; // Chuyển giao diện sang chờ
                Players.Clear();  // Xóa danh sách cũ, đợi Host gửi danh sách mới về

                // 1. Kết nối
                await _realtimeService.ConnectAsync();
                await _realtimeService.JoinRoomAsync(RoomId);

                // 2. Gửi event USER_JOINED
                // Delay nhỏ để đảm bảo kết nối ổn định trước khi gửi
                await Task.Delay(500);

                var myInfo = GetMyInfo();
                await _realtimeService.SendMessageAsync("PLAYER_JOIN", myInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi vào phòng: {ex.Message}");
                IsInLobby = false;
            }
        }

        [RelayCommand]
        private void LeaveRoom()
        {
            _realtimeService.LeaveRoom();
            IsInLobby = false;
            Players.Clear();
            IsHost = false;
        }

        [RelayCommand]
        private async Task StartGame()
        {
            if (IsHost)
            {
                await _realtimeService.SendMessageAsync("START_GAME", new { });
            }
        }

        // --- HELPERS ---
        private PlayerInfo GetMyInfo()
        {
            return new PlayerInfo
            {
                Id = _authService.CurrentUserId ?? Guid.NewGuid().ToString(), // Nếu chưa login thì random ID
                Name = CurrentUserName,
                IsHost = IsHost,
                AvatarUrl = ""
            };
        }

        private async void BroadcastLobbyState()
        {
            // Host gửi toàn bộ danh sách Players hiện tại
            await _realtimeService.SendMessageAsync("LOBBY_UPDATE", new { players = Players });
        }
    }
}