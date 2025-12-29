using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Repositories;
using EasyFlips.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyFlips.ViewModels
{
    public partial class HostGameViewModel : BaseGameViewModel
    {
        private DispatcherTimer _roundTimer;

        #region Properties

        [ObservableProperty]
        private ObservableCollection<LeaderboardEntry> _leaderboard = new ObservableCollection<LeaderboardEntry>();

        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "#FF5E57";
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private int _progressMaximum;
        [ObservableProperty] private string _correctAnswer;

        #endregion

        public HostGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
            : base(authService, supabaseService, navigationService, audioService)
        {
            QuitConfirmationMessage = "Are you sure you want to disband the room? All members will be disconnected.";
        }

        #region Initialization

        public override async Task InitializeAsync(
        string roomId,
        string classroomId,
        Deck deck,
        int timePerRound)
        {
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (deck == null || deck.Cards.Count == 0)
                throw new InvalidOperationException("The selected deck contains no cards. Game initialization failed.");

            await SubscribeToRealtimeChannel();

            await Task.Delay(150);

            if (CurrentDeck != null && CurrentDeck.Cards != null)
            {
                CurrentDeck.Cards = CurrentDeck.Cards.OrderBy(c => c.Id).ToList();
            }

            CurrentIndex = 0;
            CurrentCard = CurrentDeck.Cards.ElementAt(CurrentIndex);
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";

            var members = await _supabaseService.GetClassroomMembersWithProfileAsync(classroomId);
            UpdatePlayerList(members);

            Leaderboard.Clear();
            foreach (var mem in members)
            {
                Leaderboard.Add(new LeaderboardEntry
                {
                    UserId = mem.UserId,
                    DisplayName = mem.DisplayName,
                    AvatarUrl = mem.AvatarUrl,
                    TotalScore = 0,
                    CorrectCount = 0,
                    TotalAnswered = 0
                });
            }

            StartCountdown();

            _ = _supabaseService.StartFlashcardSessionAsync(
                ClassroomId,
                _authService.CurrentUserId,
                deck.Id,
                CurrentCard.Id,
                deck.Cards.Count,
                timePerRound
            );
        }

        #endregion

        #region Timer Logic & State Machine

        private void StartCountdown()
        {
            CurrentPhase = GamePhase.Waiting;
            StatusMessage = "Preparing to start...";
            StatusColor = "#FF5E57";
            _ = BroadcastPhaseAsync(GamePhase.Countdown, FlashcardAction.StartSession);
            StartTimer(3);
        }

        private async Task StartQuestionAsync()
        {
            CurrentPhase = GamePhase.Question;
            StatusMessage = "Answering...";
            StatusColor = "#FF5E57";

            StartTimer(TotalTimePerRound);

            _ = BroadcastPhaseAsync(GamePhase.Question, FlashcardAction.NextCard);
        }

        private async Task StartResultAsync()
        {
            CurrentPhase = GamePhase.Result;
            StatusMessage = "Showing results...";
            StatusColor = "#27AE60";

            StartTimer(10);

            _ = BroadcastPhaseAsync(GamePhase.Result, FlashcardAction.FlipCard);
        }

        private void StartTimer(int seconds)
        {
            _roundTimer?.Stop();
            TimeRemaining = seconds;
            ProgressMaximum = seconds;

            _roundTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _roundTimer.Tick += (_, __) =>
            {
                TimeRemaining--;
                if (TimeRemaining <= 0)
                {
                    _roundTimer.Stop();
                    _ = HandleTimeUpAsync();
                }
            };
            _roundTimer.Start();
        }

        private async Task HandleTimeUpAsync()
        {
            _roundTimer?.Stop();
            try
            {
                switch (CurrentPhase)
                {
                    case GamePhase.Waiting:
                        await StartQuestionAsync();
                        break;

                    case GamePhase.Question:
                        await FlipCardAsync();
                        await StartResultAsync();
                        break;

                    case GamePhase.Result:
                        await GoToNextCardOrEndAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HandleTimeUp] Error: {ex.Message}");
            }
        }

        #endregion

        #region Game Flow Actions

        [RelayCommand]
        public async Task FlipCardAsync()
        {
            ShowCard();
            CorrectAnswer = CurrentCard?.Answer;

            _ = _supabaseService.FlipCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                CurrentDeck.Id,
                CurrentCard.Id,
                CurrentIndex,
                CurrentDeck.Cards.Count,
                TimeRemaining
            );
        }

        private async Task GoToNextCardOrEndAsync()
        {
            if (CurrentIndex + 1 >= CurrentDeck.Cards.Count)
            {
                await EndGameAsync();
                return;
            }

            CurrentIndex++;
            CurrentCard = CurrentDeck.Cards.ElementAt(CurrentIndex);
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";
            IsCardFlipped = false;

            _ = BroadcastPhaseAsync(GamePhase.Question, FlashcardAction.NextCard);
            await StartQuestionAsync();
        }

        [RelayCommand] private void BackToFront() => IsCardFlipped = false;

        [RelayCommand]
        private void ShowCard()
        {
            IsCardFlipped = true;
            CorrectAnswer = CurrentCard?.Answer;
        }

        private async Task EndGameAsync()
        {
            await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            _ = BroadcastPhaseAsync(GamePhase.Finished, FlashcardAction.EndSession);
            CurrentPhase = GamePhase.Finished;

            ForceCloseWindow();
        }

        #endregion

        #region Broadcast & Realtime

        protected override async Task SubscribeToRealtimeChannel()
        {
            await _supabaseService.SubscribeToFlashcardChannelAsync(
                ClassroomId,
                OnFlashcardStateReceived,
                OnScoreReceived
            );
        }

        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
        }

        private void OnScoreReceived(ScoreSubmission submission)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Debug.WriteLine($"---------------");
                Debug.WriteLine($"[HOST-RECEIVE] ID: {submission.UserId}");
                Debug.WriteLine($"[HOST-RECEIVE] Name: {submission.DisplayName}");
                Debug.WriteLine($"[HOST-RECEIVE] New Score: {submission.Score}");
                var existingUser = Leaderboard.FirstOrDefault(x => x.UserId == submission.UserId);

                if (existingUser != null)
                {
                    Debug.WriteLine($"[HOST-LOGIC] Found existing user: {existingUser.DisplayName} (Old Score: {existingUser.TotalScore})");
                    existingUser.TotalScore = submission.Score;
                    existingUser.CorrectCount = submission.CorrectCount;
                    existingUser.TotalAnswered = submission.TotalAnswered;

                    Debug.WriteLine($"[HOST-LOGIC] Updated to New Score: {existingUser.TotalScore}");
                }
                else
                {
                    var newEntry = new LeaderboardEntry
                    {
                        UserId = submission.UserId,
                        DisplayName = submission.DisplayName,
                        TotalScore = submission.Score,
                        CorrectCount = submission.CorrectCount,
                        TotalAnswered = submission.TotalAnswered,
                        AvatarUrl = Players.FirstOrDefault(p => p.Id == submission.UserId)?.AvatarUrl
                    };
                    Leaderboard.Add(newEntry);
                }

                var sorted = Leaderboard.OrderByDescending(x => x.TotalScore).ToList();

                bool needResort = false;
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (Leaderboard[i].UserId != sorted[i].UserId)
                    {
                        needResort = true;
                        break;
                    }
                }

                if (needResort)
                {
                    Debug.WriteLine("[HOST-LOGIC] Re-sorting Leaderboard...");
                    Leaderboard.Clear();
                    foreach (var item in sorted) Leaderboard.Add(item);
                }

                Debug.WriteLine("=== CURRENT LEADERBOARD STATE (MEMORY) ===");
                foreach (var entry in Leaderboard)
                {
                    Debug.WriteLine($"User: {entry.DisplayName} | Score: {entry.TotalScore}");
                }
                Debug.WriteLine("==========================================");
            });
        }

        private async Task BroadcastPhaseAsync(GamePhase phase, FlashcardAction action)
        {
            try
            {
                var state = new FlashcardSyncState
                {
                    ClassroomId = ClassroomId,
                    DeckId = CurrentDeck?.Id,
                    CurrentCardId = CurrentCard?.Id,
                    CurrentCardIndex = CurrentIndex,
                    TotalCards = CurrentDeck.Cards.Count,
                    Phase = phase,
                    Action = action,
                    IsFlipped = IsCardFlipped,
                    TimeRemaining = TimeRemaining,
                    TriggeredBy = _authService.CurrentUserId,
                    IsSessionActive = phase != GamePhase.Finished
                };

                await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BroadcastPhase] Error: {ex.Message}");
            }
        }

        #endregion
        protected override async Task OnQuitSpecificAsync()
        {
            try
            {
                await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
                await _supabaseService.DeactivateClassroomAsync(ClassroomId);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cleaning up room: {ex.Message}", "Error");
            }
        }


        #region Close Window
        [RelayCommand]
        private async Task WindowClosing(CancelEventArgs e)
        {

            e.Cancel = true;

            await QuitGame();

        }
        #endregion
    }
}