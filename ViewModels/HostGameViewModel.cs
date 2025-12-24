using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using GamePhaseModel = EasyFlips.Models.GamePhase;

namespace EasyFlips.ViewModels
{
    internal partial class HostGameViewModel : BaseGameViewModel
    {
        [ObservableProperty] private string _correctAnswer;
        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private bool _isTimeUp;
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "Red"; // mặc định countdown màu đỏ
        [ObservableProperty] private int _submittedCount;
        [ObservableProperty] private int _progressMaximum;




        public ObservableCollection<PlayerInfo> Players { get; } = new();

        private DispatcherTimer _roundTimer;

        public HostGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService
        ) : base(authService, supabaseService, navigationService, audioService)
        {
        }

        public override async Task InitializeAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (CurrentDeck?.Cards?.Any() == true)
            {
                CurrentCard = CurrentDeck.Cards.First();
                CurrentIndex = 0;
                IsCardFlipped = false;
                CorrectAnswer = string.Empty;
                CurrentQuestionInfo = $"1/{CurrentDeck.Cards.Count}";

                await _supabaseService.StartFlashcardSessionAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    CurrentDeck.Id,
                    CurrentCard.Id,
                    CurrentDeck.Cards.Count,
                    TotalTimePerRound
                );

                // Bắt đầu countdown 3-2-1 trước khi hiển thị câu hỏi
                await StartCountdownAsync(3);
            }
        }

        private async Task StartCountdownAsync(int seconds)
        {
            TimeRemaining = seconds;
            CurrentPhase = GamePhase.Waiting;
            StatusMessage = "Chuẩn bị bắt đầu";
            StatusColor = "#FF5E57"; // đỏ
            ProgressMaximum = seconds;
            var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdownTimer.Tick += async (s, e) =>
            {
                if (TimeRemaining > 1)
                {
                    TimeRemaining--;
                }
                else
                {
                    countdownTimer.Stop();
                    CurrentPhase = GamePhase.Question;
                    // ✅ Countdown xong mới gọi ShowCard
                    await ShowCardAsync();
                }
            };
            countdownTimer.Start();

            await Task.CompletedTask;
        }
        private async Task ShowCardAsync()
        {
            CurrentPhase = GamePhase.Question;
            TimeRemaining = TotalTimePerRound;
            StatusMessage = "Trả lời câu hỏi";
            StatusColor = "#FF5E57"; // vẫn màu đỏ khi đang trả lời
            ProgressMaximum = TotalTimePerRound;
            var state = new FlashcardSyncState
            {
                ClassroomId = ClassroomId,
                Action = FlashcardAction.ShowCard,
                TriggeredBy = _authService.CurrentUserId,
                DeckId = CurrentDeck.Id,
                CurrentCardId = CurrentCard.Id,
                CurrentCardIndex = CurrentIndex,
                TotalCards = CurrentDeck.Cards.Count,
                TimeRemaining = TotalTimePerRound,
                IsSessionActive = true,
                IsPaused = false,
                Phase = GamePhaseModel.Question
            };

            await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);

            StartRoundTimer(TotalTimePerRound);
        }




        protected override Task SubscribeToRealtimeChannel() => Task.CompletedTask;

        protected override async Task OnQuitSpecificAsync()
        {
            await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            await _supabaseService.DeleteClassroomAsync(ClassroomId);
        }

        [RelayCommand]
        public async Task NextCard()
        {
            if (CurrentDeck == null) return;

            var cardsList = CurrentDeck.Cards.ToList();
            var currentIndex = cardsList.IndexOf(CurrentCard);
            var nextIndex = currentIndex + 1;

            if (nextIndex < cardsList.Count)
            {
                var nextCard = cardsList[nextIndex];
                CurrentCard = nextCard;
                CurrentIndex = nextIndex;
                IsCardFlipped = false;
                CorrectAnswer = string.Empty;
                CurrentQuestionInfo = $"{nextIndex + 1}/{cardsList.Count}";

                await _supabaseService.NextCardAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    CurrentDeck.Id,
                    nextCard.Id,
                    nextIndex,
                    cardsList.Count,
                    TotalTimePerRound
                );

              
                StopRoundTimer();
                
                await StartCountdownAsync(3);
            }
            else
            {
                await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);

                var state = new FlashcardSyncState
                {
                    ClassroomId = ClassroomId,
                    Action = FlashcardAction.EndSession,
                    TriggeredBy = _authService.CurrentUserId,
                    IsSessionActive = false,
                    IsPaused = false,
                    Phase = GamePhaseModel.Finished
                };

                await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);

                CurrentPhase = GamePhase.Finished;
                ForceCloseWindow();
            }

        }

       
        [RelayCommand]
        public async Task FlipCard()
        {
            if (CurrentCard == null) return;

            var cardsList = CurrentDeck.Cards.ToList();
            var currentIndex = cardsList.IndexOf(CurrentCard);

            await _supabaseService.FlipCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                CurrentDeck.Id,
                CurrentCard.Id,
                currentIndex,
                cardsList.Count,
                TimeRemaining
            );

            IsCardFlipped = true;
            CorrectAnswer = CurrentCard.Answer;

           
        }


        [RelayCommand]
        public void BackToFront()
        {
            if (CurrentCard == null) return;
            IsCardFlipped = false;
            CorrectAnswer = string.Empty;
            CurrentPhase = GamePhase.Question; 
        }


        private void StartRoundTimer(int seconds)
        {
            _roundTimer?.Stop();
            TimeRemaining = seconds;
            IsTimeUp = false;
            ProgressMaximum = seconds;
            _roundTimer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(1) };
            _roundTimer.Tick += async (s, e) =>
            {
                if (TimeRemaining > 0)
                {
                    TimeRemaining--;
                }
                else
                {
                    _roundTimer.Stop();
                    if (!IsCardFlipped && CurrentCard != null)
                    {
                        await FlipCard();
                    }
                    IsTimeUp = true;

                    CurrentPhase = GamePhase.Result;
                    TimeRemaining = 10;

                    var state = new FlashcardSyncState
                    {
                        ClassroomId = ClassroomId,
                        Action = FlashcardAction.FlipCard,
                        TriggeredBy = _authService.CurrentUserId,
                        DeckId = CurrentDeck.Id,
                        CurrentCardId = CurrentCard.Id,
                        CurrentCardIndex = CurrentIndex,
                        TotalCards = CurrentDeck.Cards.Count,
                        TimeRemaining = 10,
                        IsSessionActive = true,
                        IsPaused = false,
                        Phase = GamePhaseModel.Result
                    };

                    await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);

                    StartResultTimer(10);

                }
            };
            _roundTimer.Start();
        }

        private void StartResultTimer(int seconds)
        {
            _roundTimer?.Stop();
            TimeRemaining = seconds;
            StatusMessage = "Đang review kết quả";
            StatusColor = "#27AE60"; // xanh lá
            ProgressMaximum = seconds;

            _roundTimer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(1) };
            _roundTimer.Tick += async (s, e) =>
            {
                if (TimeRemaining > 0)
                {
                    TimeRemaining--;
                }
                else
                {
                    _roundTimer.Stop();

                    // Nếu còn card thì sang card mới
                    var cardsList = CurrentDeck.Cards.ToList();
                    if (CurrentIndex + 1 < cardsList.Count)
                    {
                        await NextCard();
                    }
                    else
                    {
                        // Hết bài -> EndSession sau khi chờ đủ 10s
                        var state = new FlashcardSyncState
                        {
                            ClassroomId = ClassroomId,
                            Action = FlashcardAction.EndSession,
                            TriggeredBy = _authService.CurrentUserId,
                            IsSessionActive = false,
                            IsPaused = false,
                            Phase = GamePhaseModel.Finished
                        };

                        await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);

                        CurrentPhase = GamePhase.Finished;
                        ForceCloseWindow();
                    }
                }
            };
            _roundTimer.Start();
        }


        private void StopRoundTimer() => _roundTimer?.Stop();
    }
}
