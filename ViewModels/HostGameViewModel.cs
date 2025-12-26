using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
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

        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "#FF5E57";
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private int _progressMaximum;
        [ObservableProperty] private string _correctAnswer;

        public HostGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
            : base(authService, supabaseService, navigationService, audioService)
        {
        }

        #region INIT
        public override async Task InitializeAsync(
            string roomId,
            string classroomId,
            Deck deck,
            int timePerRound)
        {
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (deck == null || deck.Cards.Count == 0)
                throw new InvalidOperationException("Deck trống, không thể bắt đầu game");

            // Subscribe kênh realtime
            await SubscribeToRealtimeChannel();
            await Task.Delay(150);
            // Khởi tạo card đầu tiên
            CurrentIndex = 0;
            CurrentCard = deck.Cards.ElementAt(CurrentIndex);
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{deck.Cards.Count}";
            // Sau khi CurrentCard được gán
            // Load danh sách thành viên
            var members = await _supabaseService.GetClassroomMembersWithProfileAsync(classroomId);
            UpdatePlayerList(members);

      
           

            // Bắt đầu countdown chuẩn bị
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

        #region TIMER FLOW
        private void StartCountdown()
        {
            CurrentPhase = GamePhase.Waiting;
            StatusMessage = "Chuẩn bị bắt đầu";
            StatusColor = "#FF5E57";


            StartTimer(3);
        }

        private async Task StartQuestionAsync()
        {
            CurrentPhase = GamePhase.Question;
            StatusMessage = "Trả lời câu hỏi";
            StatusColor = "#FF5E57";

            StartTimer(TotalTimePerRound);

            // Fire-and-forget broadcast, không block UI
            _ = BroadcastPhaseAsync(GamePhase.Question, FlashcardAction.NextCard);
        }

        private async Task StartResultAsync()
        {
            CurrentPhase = GamePhase.Result;
            StatusMessage = "Xem kết quả";
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
                    _ = HandleTimeUpAsync(); // Chỉ gọi 1 lần
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

        #region GAME FLOW
        [RelayCommand]
        public async Task FlipCardAsync()
        {
            ShowCard();

            CorrectAnswer = CurrentCard?.Answer;

            // Fire-and-forget
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

        #region BROADCAST
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
            Debug.WriteLine($"[Realtime] Phase={state.Phase}, Action={state.Action}");
        }

        private void OnScoreReceived(ScoreSubmission submission)
        {
            var player = Players.FirstOrDefault(p => p.Id == submission.UserId);
            if (player == null)
            {
                player = new PlayerInfo
                {
                    Id = submission.UserId,
                    Name = submission.DisplayName,
                    Score = 0
                };
                Players.Add(player);
            }
            player.Score += submission.Score;

            Debug.WriteLine($"[Score] {player.Name} hiện có {player.Score} điểm");
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

                // Fire-and-forget nhưng chờ send xong trong task nền
               
                        await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);
                        Debug.WriteLine($"[BroadcastPhase] Sent: Phase={phase}, Action={action}, Card={CurrentIndex + 1}/{CurrentDeck.Cards.Count}, IsFlipped={IsCardFlipped}");
                   
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BroadcastPhase] Error: {ex.Message}");
            }
        }





        #endregion
    }
}
