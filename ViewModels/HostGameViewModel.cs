using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
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
    /// <summary>
    /// ViewModel quáº£n lÃ½ luá»“ng game chÃ­nh cho Host (GiÃ¡o viÃªn).
    /// Chá»‹u trÃ¡ch nhiá»‡m: Timer, Chuyá»ƒn cÃ¢u há»i, Nháº­n Ä‘iá»ƒm tá»« Member, Broadcast tráº¡ng thÃ¡i.
    /// </summary>
    public partial class HostGameViewModel : BaseGameViewModel
    {
        private DispatcherTimer _roundTimer;

        #region Properties

        /// <summary>
        /// Danh sách bảng xếp hạng thời gian thực của các Member.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<LeaderboardEntry> _leaderboard = new ObservableCollection<LeaderboardEntry>();

        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "#FF5E57";
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private int _progressMaximum;
        [ObservableProperty] private string _correctAnswer;

        #endregion

        #region Constructor

        public HostGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
            : base(authService, supabaseService, navigationService, audioService)
        {
            QuitConfirmationMessage = "Are you sure you want to disband the room? All members will be disconnected.";
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Khá»Ÿi táº¡o game, load deck, user vÃ  báº¯t Ä‘áº§u session.
        /// </summary>
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

            // Delay nhá» Ä‘á»ƒ Ä‘áº£m báº£o káº¿t ná»‘i á»•n Ä‘á»‹nh
            await Task.Delay(150);

            // 2. Sắp xếp thẻ theo ID để đảm bảo thứ tự
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

        #region Realtime Subscription & Handlers

        protected override async Task SubscribeToRealtimeChannel()
        {
            await _supabaseService.SubscribeToFlashcardChannelAsync(
                ClassroomId,
                OnFlashcardStateReceived,
                OnScoreReceived
            );
        }

        /// <summary>
        /// Handler nháº­n state tá»« Server (Host tá»± quáº£n lÃ½ nÃªn thÆ°á»ng bá» qua hoáº·c dÃ¹ng Ä‘á»ƒ debug).
        /// </summary>
        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            // Host lÃ  nguá»“n phÃ¡t state nÃªn khÃ´ng cáº§n xá»­ lÃ½ state nháº­n vá»
        }

        /// <summary>
        /// Handler nháº­n Ä‘iá»ƒm sá»‘ tá»« Member gá»­i lÃªn.
        /// </summary>
        private void OnScoreReceived(ScoreSubmission submission)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Debug.WriteLine($"---------------");
                Debug.WriteLine($"[HOST-RECEIVE] ID: {submission.UserId}");
                Debug.WriteLine($"[HOST-RECEIVE] Name: {submission.DisplayName}");
                Debug.WriteLine($"[HOST-RECEIVE] New Score: {submission.Score}");

                // 1. TÃ¬m user trong Leaderboard
                var existingUser = Leaderboard.FirstOrDefault(x => x.UserId == submission.UserId);

                if (existingUser != null)
                {
                    Debug.WriteLine($"[HOST-LOGIC] Found existing user: {existingUser.DisplayName} (Old Score: {existingUser.TotalScore})");
                    // Cáº­p nháº­t Ä‘iá»ƒm
                    existingUser.TotalScore = submission.Score;
                    existingUser.CorrectCount = submission.CorrectCount;
                    existingUser.TotalAnswered = submission.TotalAnswered;

                    Debug.WriteLine($"[HOST-LOGIC] Updated to New Score: {existingUser.TotalScore}");
                }
                else
                {
                    // Náº¿u chÆ°a cÃ³ thÃ¬ thÃªm má»›i (phÃ²ng trÆ°á»ng há»£p member vÃ o sau)
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

                // 2. Sáº¯p xáº¿p láº¡i Leaderboard
                var sorted = Leaderboard.OrderByDescending(x => x.TotalScore).ToList();

                // Kiá»ƒm tra xem thá»© tá»± cÃ³ thay Ä‘á»•i khÃ´ng má»›i váº½ láº¡i
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

                // Debug log tráº¡ng thÃ¡i hiá»‡n táº¡i
                Debug.WriteLine("=== CURRENT LEADERBOARD STATE (MEMORY) ===");
                foreach (var entry in Leaderboard)
                {
                    Debug.WriteLine($"User: {entry.DisplayName} | Score: {entry.TotalScore}");
                }
                Debug.WriteLine("==========================================");
            });
        }

        #endregion

        #region Game Flow & State Machine

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

        private async Task EndGameAsync()
        {
            await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            _ = BroadcastPhaseAsync(GamePhase.Finished, FlashcardAction.EndSession);
            CurrentPhase = GamePhase.Finished;

            // [Gá»ŒI HÃ€M Má»šI]
            await NavigateToLeaderboardAsync();
        }

        /// <summary>
        /// Gá»­i tráº¡ng thÃ¡i game hiá»‡n táº¡i xuá»‘ng cho táº¥t cáº£ Member qua Realtime.
        /// </summary>
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

        #region Timer Logic

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

        #region User Actions (Commands)

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

        [RelayCommand]
        private void ShowCard()
        {
            IsCardFlipped = true;
            CorrectAnswer = CurrentCard?.Answer;
        }

        [RelayCommand]
        private void BackToFront() => IsCardFlipped = false;

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

        #endregion

        #region Cleanup & Closing

        /// <summary>
        /// Logic dọn dẹp khi Host chủ động thoát (Quit Game button).
        /// </summary>
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

        /// <summary>
        /// Command cháº·n sá»± kiá»‡n Ä‘Ã³ng cá»­a sá»• (NÃºt X) Ä‘á»ƒ Ä‘áº£m báº£o quy trÃ¬nh thoÃ¡t Ä‘Ãºng.
        /// </summary>
        [RelayCommand]
        private async Task WindowClosing(CancelEventArgs e)
        {
            if (_isGameEnded || _isQuitting)
            {
                return;
            }

            // 2. Nếu đang chơi dở mà bấm đóng -> Chặn lại và hỏi xác nhận
            e.Cancel = true;
            await QuitGame();
        }

        #endregion

        protected override async Task NavigateToLeaderboardAsync()
        {
            // 1. Bật cờ báo hiệu game kết thúc hợp lệ
            _isGameEnded = true;

            // 2. Chuyển đổi dữ liệu
            var finalResults = Players.ToList();

            // 3. Gọi Navigation (Navigation sẽ gọi lệnh Close cửa sổ này)
            // Khi cửa sổ đóng, nó sẽ kích hoạt sự kiện WindowClosing ở trên,
            // nhưng nhờ cờ _isGameEnded = true, nó sẽ đóng mượt mà.
            _navigationService.ShowHostLeaderboardWindow(RoomId, ClassroomId, finalResults);

            await Task.CompletedTask;
        }
    }
}