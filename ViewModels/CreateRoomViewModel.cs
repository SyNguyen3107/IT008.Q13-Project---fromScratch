using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System;

namespace EasyFlips.ViewModels
{
    public partial class CreateRoomViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IDeckRepository _deckRepository;
        private readonly IClassroomRepository _classroomRepository;
        private readonly IAuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _userSession;

        // Dữ liệu binding ra UI
        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty] private Deck _selectedDeck;
        [ObservableProperty] private int _maxPlayers = 30;
        [ObservableProperty] private int _timePerRound = 15; // Giây      
        [ObservableProperty] private int _waitTimeMinutes = 5;// 0 nghĩa là tắt đếm ngược (Bắt đầu thủ công), 5 là 5 phút

        // Constructor nhận đầy đủ các Service cần thiết
        public CreateRoomViewModel(
            INavigationService navigationService,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository,
            IAuthService authService,
            SupabaseService supabaseService,
            UserSession userSession)
        {
            _navigationService = navigationService;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;
            _authService = authService;
            _userSession = userSession;
            _supabaseService = supabaseService; // [FIX]: Gán giá trị

            LoadDecks();
        }
        //=== LẤY DECK TỪ DATABASE ===
        private async void LoadDecks()
        {
            try
            {
                var decks = await _deckRepository.GetAllAsync();
                AvailableDecks.Clear();
                foreach (var deck in decks) AvailableDecks.Add(deck);

                // Chọn mặc định cái đầu tiên
                SelectedDeck = AvailableDecks.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải danh sách Deck: {ex.Message}");
            }
        }
        //=== HÀM ĐƯỢC GỌI KHI NHẤN NÚT TẠO PHÒNG ===
        [RelayCommand]
        private async Task ConfirmCreateRoom()
        {
            if (SelectedDeck == null)
            {
                MessageBox.Show("Vui lòng chọn một bộ thẻ (Deck) để giảng dạy!", "Thông báo");
                return;
            }
            var hostId = _userSession.UserId; // Lấy ID người dùng hiện tại
            // 1. Sinh mã phòng ngẫu nhiên (6 ký tự)
            var random = new Random();
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            // Lấy 6 ký tự ngẫu nhiên tạo thành mã phòng
            string roomId = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());

            try
            {
                // Thử làm mới Token trước khi gửi (Đề phòng Token hết hạn)
                if (_supabaseService.Client.Auth.CurrentSession != null)
                {
                    await _supabaseService.Client.Auth.RefreshSession();
                }

                var newRoom = new Classroom
                {
                    RoomCode = roomId,
                    HostId = _authService.CurrentUserId,
                    DeckId = SelectedDeck.Id,
                    MaxPlayers = MaxPlayers,
                    Status = "WAITING",
                    TimePerRound = TimePerRound,
                    IsActive = true,
                    Name = $"Classroom {roomId}",
                    WaitTime = WaitTimeMinutes * 60,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                if (newRoom != null)
                {
                    // 2. Thêm Host vào danh sách thành viên với role 'owner'
                    //await _supabaseService.AddMemberAsync(newRoom.Id, hostId, "owner");

                    // 3. CHUYỂN HƯỚNG SANG HOST LOBBY
                    // Truyền RoomCode để Lobby load lại thông tin
                    await _classroomRepository.CreateClassroomAsync(newRoom);

                    await _navigationService.ShowHostLobbyWindowAsync(newRoom.RoomCode);
                }
                else
                {
                    MessageBox.Show("Lỗi khi tạo phòng, vui lòng thử lại.");
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}\nInner: {ex.InnerException?.Message}", "Lỗi Supabase");
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseWindow();
        }

        // Helper để đóng cửa sổ hiện tại
        private void CloseWindow()
        {
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }
}