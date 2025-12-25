using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using GamePhaseModel = EasyFlips.Models.GamePhase;

namespace EasyFlips.ViewModels
{
    public partial class HostGameViewModel : BaseGameViewModel
    {
        private DispatcherTimer _roundTimer;
        public GamePhase Phase { get; set; }   

        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "#FF5E57";
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private int _progressMaximum;
        [ObservableProperty]    private string _correctAnswer;


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

            CurrentIndex = 0;
            CurrentCard = deck.Cards.First();
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{deck.Cards.Count}";
            await _supabaseService.StartFlashcardSessionAsync(
                ClassroomId,
                _authService.CurrentUserId,
                deck.Id,
                CurrentCard.Id,
                deck.Cards.Count,
                timePerRound
            );

            await SubscribeToRealtimeChannel();
            StartCountdown();
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

        private void StartQuestion()
        {
            CurrentPhase = GamePhase.Question;
            StatusMessage = "Trả lời câu hỏi";
            StatusColor = "#FF5E57";

            BroadcastPhase(GamePhase.Question);
            StartTimer(TotalTimePerRound);
        }

        private void StartResult()
        {
            CurrentPhase = GamePhase.Result;
            StatusMessage = "Xem kết quả";
            StatusColor = "#27AE60";

            BroadcastPhase(GamePhase.Result);
            StartTimer(10);
        }

        private void StartTimer(int seconds)
        {
            _roundTimer?.Stop();

            TimeRemaining = seconds;
            ProgressMaximum = seconds;

            // ⚠️ FIX QUAN TRỌNG
            if (seconds <= 0)
            {
                HandleTimeUp();
                return;
            }

            _roundTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _roundTimer.Tick += (_, __) =>
            {
                TimeRemaining--;

                if (TimeRemaining <= 0)
                {
                    _roundTimer.Stop();
                    HandleTimeUp();
                }
            };

            _roundTimer.Start();
        }

        private async void HandleTimeUp()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Waiting:
                    StartQuestion();
                    break;

                case GamePhase.Question:
                    _ = FlipCard();
                    StartResult();

                    break;

                case GamePhase.Result:
                    _ = NextCardOrEnd();
                    break;
            }
        }

        #endregion

        #region GAME FLOW

        [RelayCommand]
        public async Task FlipCard()
        {
            ShowCard();
            CorrectAnswer = CurrentCard?.Answer;
            await _supabaseService.FlipCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                CurrentDeck.Id,
                CurrentCard.Id,
                CurrentIndex,
                CurrentDeck.Cards.Count,
                TimeRemaining
            );
        }

        private async Task NextCardOrEnd()
        {
            if (CurrentIndex + 1 >= CurrentDeck.Cards.Count)
            {
                await EndGame();
                return;
            }

            CurrentIndex++;
            var cardsList = CurrentDeck.Cards.ToList();
            CurrentCard = cardsList[CurrentIndex];
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";
            IsCardFlipped = false;

            StartCountdown();
        }
        [RelayCommand]
        private void BackToFront()
        {
            // Đặt lại trạng thái để hiển thị mặt trước
            IsCardFlipped = false;

            
        }
        [RelayCommand]
        private void ShowCard()
        {
            // Chỉ lật thẻ trên UI, không gửi đi đâu cả
            IsCardFlipped = true;
            CorrectAnswer = CurrentCard?.Answer;
        }


        private async Task EndGame()
        {
            await _supabaseService.EndFlashcardSessionAsync(
                ClassroomId,
                _authService.CurrentUserId);

            await BroadcastPhase(GamePhase.Finished);
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
            // Bạn có thể xử lý trạng thái đồng bộ tại đây nếu cần
            System.Diagnostics.Debug.WriteLine($"[Realtime] Phase: {state.Phase}, Action: {state.Action}");
        }

        private void OnScoreReceived(ScoreSubmission submission)
        {
            // Tìm player theo UserId
            var player = Players.FirstOrDefault(p => p.Id == submission.UserId);

            if (player == null)
            {
                // Nếu chưa có thì thêm mới
                player = new PlayerInfo
                {
                    Id = submission.UserId,
                    Name = submission.DisplayName,
                    Score = 0
                };
                Players.Add(player);
            }

            // Cộng điểm
            player.Score += submission.Score;

            // Debug log
            System.Diagnostics.Debug.WriteLine($"[Score] {player.Name} hiện có {player.Score} điểm");
        }



        private async Task BroadcastPhase(GamePhase phase)
        {
            FlashcardPhase syncPhase = phase switch
            {
                GamePhase.Question => FlashcardPhase.Question,
                GamePhase.Result => FlashcardPhase.Result,
                GamePhase.Finished => FlashcardPhase.Finished,
                _ => FlashcardPhase.None
            };

            var state = new FlashcardSyncState
            {
                ClassroomId = ClassroomId,
                DeckId = CurrentDeck.Id,
                CurrentCardId = CurrentCard?.Id,
                CurrentCardIndex = CurrentIndex,
                TotalCards = CurrentDeck.Cards.Count,
                Phase = phase, 



                Action = phase switch
                {
                    GamePhase.Question => FlashcardAction.ShowCard,
                    GamePhase.Result => FlashcardAction.FlipCard,
                    GamePhase.Finished => FlashcardAction.EndSession,
                    _ => FlashcardAction.None
                },

                IsFlipped = IsCardFlipped,
                TimeRemaining = TimeRemaining,
                TriggeredBy = _authService.CurrentUserId,
                IsSessionActive = phase != GamePhase.Finished
            };

            await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);
        }

        #endregion
    }
}
