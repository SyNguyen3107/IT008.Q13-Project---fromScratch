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
        private readonly SupabaseService _supabaseService; // [FIX]: Thêm field này

        // Dữ liệu binding ra UI
        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty] private Deck _selectedDeck;
        [ObservableProperty] private int _maxPlayers = 30;
        [ObservableProperty] private int _timePerRound = 15; // Giây

        // Constructor nhận đầy đủ các Service cần thiết
        public CreateRoomViewModel(
            INavigationService navigationService,
            IDeckRepository deckRepository,
            IClassroomRepository classroomRepository,
            IAuthService authService,
            SupabaseService supabaseService) // [FIX]: Inject thêm SupabaseService
        {
            _navigationService = navigationService;
            _deckRepository = deckRepository;
            _classroomRepository = classroomRepository;
            _authService = authService;
            _supabaseService = supabaseService; // [FIX]: Gán giá trị

            LoadDecks();
        }

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

        [RelayCommand]
        private async Task ConfirmCreateRoom()
        {
            if (SelectedDeck == null)
            {
                MessageBox.Show("Vui lòng chọn một bộ thẻ (Deck) để giảng dạy!", "Thông báo");
                return;
            }

            // 1. Sinh mã phòng ngẫu nhiên (6 ký tự)
            var random = new Random();
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string roomId = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());

            try
            {
                // [FIX]: Thử làm mới Token trước khi gửi (Đề phòng Token hết hạn)
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
                    Name = $"Lớp học {roomId}"
                };

                // Gọi Repository (Bây giờ Database đã cho phép 'anon' nên chắc chắn sẽ qua)
                await _classroomRepository.CreateClassroomAsync(newRoom);

                _navigationService.ShowLobbyWindow(roomId, isHost: true, deck: SelectedDeck, MaxPlayers);
                CloseWindow();
            }
            catch (Exception ex)
            {
                // Nếu vẫn lỗi, in chi tiết Exception ra để xem
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