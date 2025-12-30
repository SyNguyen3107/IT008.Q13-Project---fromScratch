using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class HostLobbyViewModel : BaseLobbyViewModel
    {
        private readonly IDeckRepository _deckRepository;
        private const int HEARTBEAT_TIMEOUT_SECONDS = 15;
        private bool _isQuitting = false;

        [ObservableProperty] private Deck _selectedDeck;
        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        public HostLobbyViewModel(
            IAuthService authService,
            INavigationService navigationService,
            UserSession userSession,
            IClassroomRepository classroomRepository,
            SupabaseService supabaseService,
            AudioService audioService,
            IDeckRepository deckRepository)
            : base(authService, navigationService, userSession, classroomRepository, supabaseService, audioService)
        {
            _deckRepository = deckRepository;
        }

        protected override async Task OnInitializeSpecificAsync(Classroom roomInfo)
        {
            // 1. Load danh sách bộ bài cho Host chọn
            var decks = await _deckRepository.GetAllAsync();
            AvailableDecks.Clear();
            foreach (var d in decks) AvailableDecks.Add(d);

            // Chọn mặc định bộ đầu tiên hoặc bộ đã lưu trong setting phòng (nếu có logic đó)
            if (!string.IsNullOrEmpty(roomInfo.DeckId))
            {
                SelectedDeck = decks.FirstOrDefault(d => d.Id == roomInfo.DeckId);
            }
            else
            {
                // Nếu chưa có thì chọn mặc định bộ đầu tiên
                SelectedDeck = decks.FirstOrDefault();
            }

            // 2. Kích hoạt Timer nếu có thời gian chờ
            if (AutoStartSeconds > 0)
            {
                IsAutoStartActive = true;
                _autoStartTimer.Start();
            }
        }

        protected override async Task OnPollingSpecificAsync(List<MemberWithProfile> currentMembers)
        {
            // Logic Host: Kiểm tra xem ai bị AFK để Kick
            var myId = _authService.CurrentUserId ?? _userSession.UserId;
            var now = DateTime.Now;
            var usersToKick = new List<string>();

            foreach (var member in currentMembers)
            {
                if (member.UserId == myId) continue; // Không tự kick mình

                // Chuẩn hóa về UTC để so sánh
                DateTime lastActiveUtc = member.LastActive.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(member.LastActive, DateTimeKind.Utc)
                    : member.LastActive.ToUniversalTime();

                // Heartbeat timeout check
                if ((now - lastActiveUtc).TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
                {
                    usersToKick.Add(member.UserId);
                }
            }

            // Thực hiện Kick
            foreach (var userId in usersToKick)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoKick] Kicking user: {userId}");
                await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, userId);

                // UI sẽ tự cập nhật ở lần polling tiếp theo hoặc Polling Common Logic
            }
        }

        protected override void OnTimerFinished()
        {
            // Hết giờ -> Host tự động bắt đầu game
            StartGameCommand.Execute(null);
        }

        protected override Deck GetSelectedDeck() => SelectedDeck;

        [RelayCommand]
        private async Task StartGame()
        {
            try
            {
                if (SelectedDeck == null)
                {
                    MessageBox.Show("Vui lòng chọn một bộ thẻ trước khi bắt đầu!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StopAutoStart();
                StopPolling();

                // ⚠️ BƯỚC 1: Upload Deck lên Supabase Cloud
                // Vì Deck lưu ở Local SQLite, Member không thể truy cập trực tiếp
                // Cần upload lên Cloud để Member có thể fetch
                System.Diagnostics.Debug.WriteLine($"[Host] 🔄 Đang upload deck lên cloud: {SelectedDeck.Name}");
                
                // Load full deck với cards từ local DB
                var fullDeck = await _deckRepository.GetByIdAsync(SelectedDeck.Id);
                if (fullDeck == null || fullDeck.Cards == null || !fullDeck.Cards.Any())
                {
                    MessageBox.Show("Bộ thẻ không có card nào! Vui lòng thêm card trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StartPolling();
                    return;
                }

                var uploadSuccess = await _supabaseService.UploadDeckToCloudAsync(fullDeck);


                if (!uploadSuccess)
                {
                    MessageBox.Show("Không thể upload bộ thẻ lên server. Vui lòng thử lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    StartPolling();
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Upload deck thành công!");

                // ⚠️ BƯỚC 2: Lưu DeckId vào Classroom
                System.Diagnostics.Debug.WriteLine($"[Host] 🔄 Đang lưu DeckId: {SelectedDeck.Id}");
                await _classroomRepository.UpdateClassroomSettingsAsync(
                   _realClassroomIdUUID,
                   SelectedDeck.Id,
                   MaxPlayers,
                   TimePerRound,
                   TotalWaitTime
               );
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Đã lưu DeckId thành công");

                // ⚠️ BƯỚC 3: Cập nhật status PLAYING (SAU CÙNG)
                System.Diagnostics.Debug.WriteLine($"[Host] 🔄 Đang cập nhật status sang PLAYING...");
                await _classroomRepository.UpdateStatusAsync(_realClassroomIdUUID, "PLAYING");
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Đã cập nhật status thành công");

                // Cập nhật SelectedDeck với full cards
                SelectedDeck = fullDeck;

                NavigateToGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start session: {ex.Message}", "Error");
                StartPolling(); // Resume polling nếu lỗi
            }
        }
    


        protected override async void NavigateToGame()
        {
            CanCloseWindow = true;

            Deck deckToPass = GetSelectedDeck(); // luôn có Deck

            await _navigationService.ShowHostGameWindowAsync(
                RoomId,
                _realClassroomIdUUID,
                deckToPass,
                TimePerRound
            );

            ForceCloseWindow();
        }

        [RelayCommand]
        private async Task WindowClosing(CancelEventArgs e)
        {

            if (!CanCloseWindow)
            {
                e.Cancel = true;

                // 2. Gọi hàm giải tán phòng của bạn
                // Hàm này sẽ tự lo việc hỏi Confirm, xóa DB và tự đóng Window sau khi xong
                await CloseRoom();
            }
        }

        [RelayCommand]
        private async Task CloseRoom()
        {
            if (_isQuitting) return;
            if (MessageBox.Show("Are you sure to end this session?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _isQuitting = true;
                    StopPolling();
                    await _classroomRepository.DeleteClassroomAsync(RoomId);

                    // MỞ LẠI MAIN WINDOW
                    _navigationService.ShowMainWindow();

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi giải tán phòng: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            try
            {
                // Mở cửa sổ setting (tái sử dụng SettingsViewModel cũ)
                var settingsVm = new SettingsViewModel(_deckRepository, SelectedDeck, MaxPlayers, TimePerRound, TotalWaitTime);
                var settingsWindow = new Views.SettingsWindow(settingsVm);

                if (settingsWindow.ShowDialog() == true)
                {
                    // Cập nhật lên DB
                    await _classroomRepository.UpdateClassroomSettingsAsync(
                        _realClassroomIdUUID,
                        settingsVm.SelectedDeck?.Id,
                        settingsVm.MaxPlayers,
                        settingsVm.TimePerRound,
                        settingsVm.WaitTimeMinutes * 60
                    );

                    // Cập nhật Local
                    SelectedDeck = settingsVm.SelectedDeck; // Quan trọng
                    TotalWaitTime = settingsVm.WaitTimeMinutes * 60;
                    AutoStartSeconds = TotalWaitTime;
                    MaxPlayers = settingsVm.MaxPlayers;
                    TimePerRound = settingsVm.TimePerRound;

                    // Restart timer
                    IsAutoStartActive = true;
                    _autoStartTimer.Start();

                    MessageBox.Show("Cập nhật cài đặt thành công.", "Thành công");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở cài đặt: {ex.Message}");
            }
        }


    }
}