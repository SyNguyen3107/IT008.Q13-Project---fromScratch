using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberLeaderboardViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;
        private readonly INavigationService _navigationService;

        private readonly IClassroomRepository _classroomRepository;

        // Các thuộc tính Binding UI
        [ObservableProperty] private PlayerInfo _firstPlace;
        [ObservableProperty] private PlayerInfo _secondPlace;
        [ObservableProperty] private PlayerInfo _thirdPlace;
        public ObservableCollection<PlayerInfo> RestOfPlayers { get; } = new();
        public Action CloseAction { get; set; }
        private bool _hasLeft = false;

        private string _roomId;
        private string _classroomId;

        // [CẬP NHẬT CONSTRUCTOR] Thêm IClassroomRepository
        public MemberLeaderboardViewModel(
            SupabaseService supabaseService,
            INavigationService navigationService,
            IClassroomRepository classroomRepository)
        {
            _supabaseService = supabaseService;
            _navigationService = navigationService;
            _classroomRepository = classroomRepository;
        }

        public async Task InitializeAsync(string roomId, string classroomId)
        {
            _roomId = roomId; // Lưu lại roomId (thường là Code)
            _classroomId = classroomId; // Lưu lại classroomId (UUID)

            // 1. Tải bảng xếp hạng từ Database
            var dbPlayers = await _supabaseService.GetLeaderboardFromDbAsync(classroomId);

            // 2. Cập nhật UI
            if (dbPlayers != null)
            {
                UpdateLeaderboardUI(dbPlayers);
            }

            // [FIX QUAN TRỌNG] 3. Đăng ký lắng nghe tín hiệu từ Host
            // Nếu thiếu dòng này, hàm OnSignalReceived bên dưới sẽ KHÔNG BAO GIỜ chạy
            await _supabaseService.SubscribeToControlSignalsAsync(_classroomId, OnSignalReceived);
        }

        private void UpdateLeaderboardUI(System.Collections.Generic.List<PlayerInfo> sortedList)
        {
            // Reset
            FirstPlace = null; SecondPlace = null; ThirdPlace = null;
            RestOfPlayers.Clear();

            // Sắp xếp
            sortedList = sortedList.OrderByDescending(p => p.Score).ToList();

            // Gán Rank
            for (int i = 0; i < sortedList.Count; i++) sortedList[i].Rank = i + 1;

            // Phân bổ Podium (Bục vinh quang)
            if (sortedList.Count > 0) FirstPlace = sortedList[0];
            if (sortedList.Count > 1) SecondPlace = sortedList[1];
            if (sortedList.Count > 2) ThirdPlace = sortedList[2];

            // Danh sách còn lại
            if (sortedList.Count > 3)
            {
                foreach (var p in sortedList.Skip(3)) RestOfPlayers.Add(p);
            }
        }

        private void OnSignalReceived(GameControlSignal signal)
        {
            if (_hasLeft) return;
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (signal == GameControlSignal.ReturnToLobby)
                {
                    // Host bấm New Game -> Về Lobby
                    try
                    {
                        var room = await _classroomRepository.GetClassroomAsync(_classroomId);
                        if (room != null)
                        {
                            await _navigationService.ShowMemberLobbyWindowAsync(room.RoomCode);
                        }
                    }
                    catch
                    {
                        // Fallback nếu lỗi: Thử dùng _roomId nếu nó tình cờ là Code
                        await _navigationService.ShowMemberLobbyWindowAsync(_roomId);
                    }
                    CloseAction?.Invoke();
                }
                else if (signal == GameControlSignal.CloseRoom)
                {
                    // Host đóng phòng -> Về Home
                    MessageBox.Show("Host has ended this session. Returning to home...", "Notification");

                    await _supabaseService.LeaveFlashcardSyncChannelAsync(_classroomId);
                    _hasLeft = true;
                    _navigationService.ShowMainWindow();
                    CloseAction?.Invoke();
                }
            });
        }

        [RelayCommand]
        private async Task LeaveRoom()
        {
            if (MessageBox.Show("Do you want to return to Main window?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _hasLeft = true;
                // [FIX] Hủy đăng ký Realtime khi chủ động rời đi
                await _supabaseService.LeaveFlashcardSyncChannelAsync(_classroomId);

                _navigationService.ShowMainWindow();
            }
        }
    }
}