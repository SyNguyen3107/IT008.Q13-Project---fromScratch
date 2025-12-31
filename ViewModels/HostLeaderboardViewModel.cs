using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging; // Cho Save Image
using System.IO;

namespace EasyFlips.ViewModels
{
    public partial class HostLeaderboardViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;
        private readonly INavigationService _navigationService;
        private readonly IClassroomRepository _classroomRepository;

        [ObservableProperty] private PlayerInfo _firstPlace;
        [ObservableProperty] private PlayerInfo _secondPlace;
        [ObservableProperty] private PlayerInfo _thirdPlace;
        public ObservableCollection<PlayerInfo> RestOfPlayers { get; } = new();

        private string _roomId;
        private string _classroomId;

        public Action CloseAction { get; set; }

        public HostLeaderboardViewModel(
            SupabaseService supabaseService,
            INavigationService navigationService,
            IClassroomRepository classroomRepository)
        {
            _supabaseService = supabaseService;
            _navigationService = navigationService;
            _classroomRepository = classroomRepository;
        }

        // Tham số cachedPlayersFromGame có thể dùng làm placeholder trong lúc chờ tải DB
        public async Task InitializeAsync(string roomId, string classroomId, System.Collections.Generic.List<PlayerInfo> cachedPlayersFromGame)
        {
            _roomId = roomId;
            _classroomId = classroomId;

            // [CHIẾN LƯỢC PULL] Tải dữ liệu chính xác từ Database
            // Đảm bảo tính nhất quán giữa Host và Member
            var dbPlayers = await _supabaseService.GetLeaderboardFromDbAsync(_classroomId);

            // Nếu DB chưa kịp cập nhật hoặc rỗng (hiếm), dùng tạm cache từ Game gửi sang
            var finalPlayers = (dbPlayers != null && dbPlayers.Any()) ? dbPlayers : cachedPlayersFromGame;

            // Cập nhật UI
            UpdateLeaderboardUI(finalPlayers);
        }

        private void UpdateLeaderboardUI(System.Collections.Generic.List<PlayerInfo> sortedList)
        {
            // Reset
            FirstPlace = null; SecondPlace = null; ThirdPlace = null;
            RestOfPlayers.Clear();

            // Sắp xếp lại cho chắc chắn
            sortedList = sortedList.OrderByDescending(p => p.Score).ToList();

            // Gán Rank
            for (int i = 0; i < sortedList.Count; i++) sortedList[i].Rank = i + 1;

            // Phân bổ Podium
            if (sortedList.Count > 0) FirstPlace = sortedList[0];
            if (sortedList.Count > 1) SecondPlace = sortedList[1];
            if (sortedList.Count > 2) ThirdPlace = sortedList[2];

            // Danh sách còn lại
            if (sortedList.Count > 3)
            {
                foreach (var p in sortedList.Skip(3)) RestOfPlayers.Add(p);
            }
        }

        [RelayCommand]
        private async Task PlayNewGame()
        {
            try
            {

                await _supabaseService.ReactivateClassroomAsync(_classroomId);

                _ = _supabaseService.SendGameControlSignalAsync(_classroomId, GameControlSignal.ReturnToLobby);

                // 3. Host về Lobby ngay lập tức
                var room = await _classroomRepository.GetClassroomAsync(_classroomId);
                if (room != null)
                {
                    await _navigationService.ShowHostLobbyWindowAsync(room.RoomCode);
                }
                CloseAction?.Invoke();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CloseRoom()
        {
            if (MessageBox.Show("Are you sure to end this session?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Tạo Task gửi tin nhắn nhưng KHÔNG await ngay
                    var sendSignalTask = _supabaseService.SendGameControlSignalAsync(_classroomId, GameControlSignal.CloseRoom);
                    // 2. Tạo Task đếm ngược 2 giây
                    var timeoutTask = Task.Delay(2000);
                    // 3. Đua: Cái nào xong trước thì đi tiếp
                    // Nếu mạng tốt, sendSignalTask xong ngay. Nếu mạng lag, sau 2s timeoutTask sẽ xong.
                    await Task.WhenAny(sendSignalTask, timeoutTask);

                    // 4. Update DB -> Inactive (Quan trọng nhất)
                    await _supabaseService.DeactivateClassroomAsync(_classroomId);
                    // 5. Host về Home
                    _navigationService.ShowMainWindow();

                    CloseAction?.Invoke();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi đóng phòng: {ex.Message}");
                    // Kể cả lỗi cũng cho về Home để tránh kẹt
                    _navigationService.ShowMainWindow();
                }
            }
        }
    }
}