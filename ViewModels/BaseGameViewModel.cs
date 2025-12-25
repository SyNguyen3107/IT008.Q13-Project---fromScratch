using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    /// <summary>
    /// Trạng thái vòng chơi
    /// </summary>
    

    /// <summary>
    /// Lớp ViewModel nền cho Host & Member
    /// </summary>
    public abstract partial class BaseGameViewModel : ObservableObject
    {
        #region Services
        protected readonly IAuthService _authService;
        protected readonly SupabaseService _supabaseService;
        protected readonly INavigationService _navigationService;
        protected readonly AudioService _audioService;
        #endregion

        #region Shared Game Data

        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _classroomId;

        [ObservableProperty] private Deck _currentDeck;
        [ObservableProperty] private Card _currentCard;

        [ObservableProperty] private int _currentIndex;
        [ObservableProperty] private int _totalCards;

        [ObservableProperty] private int _timeRemaining;
        [ObservableProperty] private int _totalTimePerRound;

        [ObservableProperty] private GamePhase _currentPhase = GamePhase.Waiting;

        public string ProgressText => $"{CurrentIndex + 1}/{TotalCards}";

        public ObservableCollection<PlayerInfo> Players { get; } = new();

        #endregion

        #region Constructor

        protected BaseGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
        {
            _authService = authService;
            _supabaseService = supabaseService;
            _navigationService = navigationService;
            _audioService = audioService;
        }

        #endregion

        #region Initialization

        public virtual async Task InitializeAsync(
            string roomId,
            string classroomId,
            Deck deck,
            int timePerRound)
        {
            RoomId = roomId;
            ClassroomId = classroomId;
            CurrentDeck = deck;
            TotalTimePerRound = timePerRound;
            TimeRemaining = 3;

            if (deck != null)
                TotalCards = deck.Cards.Count;

            var members = await _supabaseService.GetClassroomMembersWithProfileAsync(classroomId);
            UpdatePlayerList(members);
        }

        #endregion

        #region Player Handling

        protected void UpdatePlayerList(System.Collections.Generic.List<MemberWithProfile> members)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var m in members)
                {
                    if (!Players.Any(p => p.Id == m.UserId))
                    {
                        Players.Add(new PlayerInfo
                        {
                            Id = m.UserId,
                            Name = m.DisplayName,
                            AvatarUrl = string.IsNullOrEmpty(m.AvatarUrl)
                                ? "/Resources/user.png"
                                : m.AvatarUrl,
                            IsHost = m.Role == "owner" || m.Role == "host"
                        });
                    }
                }
            });
        }

        #endregion

        #region Navigation & Exit

        protected virtual async Task EndAndNavigateToLeaderboardAsync()
        {
            await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);

            _navigationService.ShowLeaderBoardWindow(
                RoomId,
                ClassroomId,
                Players
            );

            ForceCloseWindow();
        }

        [RelayCommand]
        public async Task QuitGame()
        {
            if (MessageBox.Show(
                "Bạn có chắc muốn thoát trò chơi?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await OnQuitSpecificAsync();
            await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);
            _navigationService.ShowMainWindow();
            ForceCloseWindow();
        }

        protected virtual Task OnQuitSpecificAsync() => Task.CompletedTask;

        protected void ForceCloseWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (w.DataContext == this)
                    {
                        w.Close();
                        break;
                    }
                }
            });
        }

        #endregion

        #region Realtime

        protected abstract Task SubscribeToRealtimeChannel();

        #endregion
    }
}
