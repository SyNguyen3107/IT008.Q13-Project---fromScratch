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
    /// <summary>
    /// Base ViewModel containing shared logic for both Host and Member views.
    /// Manages common state (Deck, Card, Timer), Player list, and Navigation.
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

        // Collection for UI binding of connected players
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

        /// <summary>
        /// Initializes common game data.
        /// </summary>
        /// <param name="roomId">Display Room ID</param>
        /// <param name="classroomId">Database Classroom ID</param>
        /// <param name="deck">The Flashcard Deck to be used</param>
        /// <param name="timePerRound">Duration for each question</param>
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
            TimeRemaining = 3; // Default countdown

            if (deck != null)
            {
                TotalCards = deck.Cards.Count;
            }
        }

        #endregion

        #region Player Handling

        /// <summary>
        /// Updates the ObservableCollection of players based on data fetched from Supabase.
        /// Ensures UI thread safety using Dispatcher.
        /// </summary>
        /// <param name="members">List of members fetched from DB</param>
        protected void UpdatePlayerList(List<MemberWithProfile> members)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var m in members)
                {
                    // Add only if not already in the list
                    if (!Players.Any(p => p.Id == m.UserId))
                    {
                        Players.Add(new PlayerInfo
                        {
                            Id = m.UserId,
                            Name = m.DisplayName,
                            // Fallback to default image if AvatarUrl is missing
                            AvatarUrl = string.IsNullOrEmpty(m.AvatarUrl)
                                ? "/Resources/user.png" // Consider using pack://application... for robustness
                                : m.AvatarUrl,
                            IsHost = m.Role == "owner" || m.Role == "host"
                        });
                    }
                }
            });
        }

        #endregion

        #region Navigation & Exit

        /// <summary>
        /// Cleanly disconnects from Realtime channel and navigates to the Leaderboard screen.
        /// </summary>
        protected abstract Task NavigateToLeaderboardAsync();

        /// <summary>
        /// Handles the "Quit Game" action with confirmation dialog.
        /// </summary>
        // Trong BaseGameViewModel.cs

        [ObservableProperty]
        private string _quitConfirmationMessage = "Are you sure you want to quit the game?";

        protected bool _isGameEnded = false;

        //biến cờ kiểm soát trạng thái đang thoát
        protected bool _isQuitting = false;
        [RelayCommand]
        public async Task QuitGame()
        {
            // Nếu đang thoát rồi thì chặn ngay, tránh hiện dialog lần 2
            if (_isQuitting) return;
            // Title: "Confirmation"
            if (MessageBox.Show(
                QuitConfirmationMessage,
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _isQuitting = true;
            
            await OnQuitSpecificAsync();
            await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);


            _navigationService.ShowMainWindow();

            if (CloseWindowAction != null)
                RequestCloseWindow();
            else
                ForceCloseWindow();
        }

        /// <summary>
        /// Hook for child classes to perform specific cleanup before quitting.
        /// </summary>
        protected virtual Task OnQuitSpecificAsync() => Task.CompletedTask;

        /// <summary>
        /// Forces the current window associated with this ViewModel to close.
        /// </summary>
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

        #region Realtime Contract

        /// <summary>
        /// Abstract method to enforce Realtime subscription implementation in child classes.
        /// </summary>
        protected abstract Task SubscribeToRealtimeChannel();

        #endregion
        public Action? CloseWindowAction { get; set; }

        // [BỔ SUNG] Hàm gọi Action đóng cửa sổ
        protected void RequestCloseWindow()
        {
            CloseWindowAction?.Invoke();
        }
    }
}