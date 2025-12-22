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

namespace EasyFlips.ViewModels
{
    public partial class HostLobbyViewModel : BaseLobbyViewModel
    {
        private readonly IDeckRepository _deckRepository;
        private const int HEARTBEAT_TIMEOUT_SECONDS = 15;

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
                StopAutoStart();
                StopPolling();

                // Cập nhật trạng thái phòng sang PLAYING
                await _classroomRepository.UpdateStatusAsync(_realClassroomIdUUID, "PLAYING");

                // Cập nhật Deck đã chọn lần cuối lên DB (để Member tải về đúng Deck)
                if (SelectedDeck != null)
                {
                    await _classroomRepository.UpdateClassroomSettingsAsync(
                       _realClassroomIdUUID,
                       SelectedDeck.Id,
                       MaxPlayers,
                       TimePerRound,
                       TotalWaitTime
                   );
                }

                NavigateToGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi bắt đầu game: {ex.Message}", "Lỗi");
                StartPolling(); // Resume polling nếu lỗi
            }
        }

        [RelayCommand]
        private async Task CloseRoom()
        {
            if (MessageBox.Show("Bạn có chắc muốn giải tán phòng?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
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