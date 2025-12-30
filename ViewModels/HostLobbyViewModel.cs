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
            var decks = await _deckRepository.GetAllAsync();
            AvailableDecks.Clear();
            foreach (var d in decks) AvailableDecks.Add(d);

            if (!string.IsNullOrEmpty(roomInfo.DeckId))
            {
                SelectedDeck = decks.FirstOrDefault(d => d.Id == roomInfo.DeckId);
            }
            else
            {
                SelectedDeck = decks.FirstOrDefault();
            }

            if (AutoStartSeconds > 0)
            {
                IsAutoStartActive = true;
                _autoStartTimer.Start();
            }
        }

        protected override async Task OnPollingSpecificAsync(List<MemberWithProfile> currentMembers)
        {
            var myId = _authService.CurrentUserId ?? _userSession.UserId;
            var now = DateTime.Now;
            var usersToKick = new List<string>();

            foreach (var member in currentMembers)
            {
                if (member.UserId == myId) continue;
                DateTime lastActiveUtc = member.LastActive.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(member.LastActive, DateTimeKind.Utc)
                : member.LastActive.ToUniversalTime();

                if ((now - lastActiveUtc).TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
                {
                    usersToKick.Add(member.UserId);
                }
            }

            foreach (var userId in usersToKick)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoKick] Kicking user: {userId}");
                await _classroomRepository.RemoveMemberAsync(_realClassroomIdUUID, userId);

            }
        }

        protected override void OnTimerFinished()
        {
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
                    MessageBox.Show("Please select a deck before starting!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StopAutoStart();
                StopPolling();


                var fullDeck = await _deckRepository.GetByIdAsync(SelectedDeck.Id);
                if (fullDeck == null || fullDeck.Cards == null || !fullDeck.Cards.Any())
                {
                    MessageBox.Show("This deck is empty! Please add some cards first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StartPolling();
                    return;
                }

                var uploadSuccess = await _supabaseService.UploadDeckToCloudAsync(fullDeck);


                if (!uploadSuccess)
                {
                    MessageBox.Show("Failed to upload the deck to the server. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StartPolling();
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Upload deck thành công!");

                System.Diagnostics.Debug.WriteLine($"[Host] 🔄 Đang lưu DeckId: {SelectedDeck.Id}");
                await _classroomRepository.UpdateClassroomSettingsAsync(
                   _realClassroomIdUUID,
                   SelectedDeck.Id,
                   MaxPlayers,
                   TimePerRound,
                   TotalWaitTime
               );
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Đã lưu DeckId thành công");

                System.Diagnostics.Debug.WriteLine($"[Host] 🔄 Đang cập nhật status sang PLAYING...");
                await _classroomRepository.UpdateStatusAsync(_realClassroomIdUUID, "PLAYING");
                System.Diagnostics.Debug.WriteLine($"[Host] ✅ Đã cập nhật status thành công");

                SelectedDeck = fullDeck;

                NavigateToGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start the game: {ex.Message}", "Error");
                StartPolling();
            }
        }



        protected override async void NavigateToGame()
        {
            CanCloseWindow = true;

            Deck deckToPass = GetSelectedDeck();
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

                    _navigationService.ShowMainWindow();

                    CanCloseWindow = true;
                    ForceCloseWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to disband the room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            try
            {
                var settingsVm = new SettingsViewModel(_deckRepository, SelectedDeck, MaxPlayers, TimePerRound, TotalWaitTime);
                var settingsWindow = new Views.SettingsWindow(settingsVm);

                var parentWindow = Application.Current.Windows
                                          .OfType<Window>()
                                          .FirstOrDefault(w => w.IsActive && w != settingsWindow);
                if (parentWindow != null)
                {
                    settingsWindow.Owner = parentWindow;
                    settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (settingsWindow.ShowDialog() == true)
                {
                    await _classroomRepository.UpdateClassroomSettingsAsync(
                    _realClassroomIdUUID,
                    settingsVm.SelectedDeck?.Id,
                    settingsVm.MaxPlayers,
                    settingsVm.TimePerRound,
                    settingsVm.WaitTimeMinutes * 60
                );

                    SelectedDeck = settingsVm.SelectedDeck; TotalWaitTime = settingsVm.WaitTimeMinutes * 60;
                    AutoStartSeconds = TotalWaitTime;
                    MaxPlayers = settingsVm.MaxPlayers;
                    TimePerRound = settingsVm.TimePerRound;

                    IsAutoStartActive = true;
                    _autoStartTimer.Start();

                    MessageBox.Show("Settings updated successfully.", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings: {ex.Message}", "Error");
            }
        }


    }
}