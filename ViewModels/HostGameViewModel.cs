using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Repositories;
using EasyFlips.Services;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyFlips.ViewModels
{
    internal partial class HostGameViewModel : BaseGameViewModel
    {
        [ObservableProperty] private string _correctAnswer;
        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private bool _isTimeUp;
        [ObservableProperty] private string _currentQuestionInfo;

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
                System.Diagnostics.Debug.WriteLine($"[DEBUG] FrontImagePath = {CurrentCard?.FrontImagePath}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] BackImagePath = {CurrentCard?.BackImagePath}");
            

            StartRoundTimer(TotalTimePerRound);
            }
        }


        protected override Task SubscribeToRealtimeChannel()
        {
            // Host không cần lắng nghe riêng
            return Task.CompletedTask;
           
        }
        

        protected override async Task OnQuitSpecificAsync()
        {
            //Chỉ dùng để test
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
                StartRoundTimer(TotalTimePerRound); 
            }
            else
            {
                await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
                CurrentPhase = GamePhase.Finished;
            }
        }


        [RelayCommand]
        public async Task FlipCard()
        {
            if (CurrentCard == null) return;

            var cardsList = CurrentDeck.Cards.ToList();
            var currentIndex = cardsList.IndexOf(CurrentCard);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] FrontImagePath = {CurrentCard.FrontImagePath}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] BackImagePath = {CurrentCard.BackImagePath}");
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
        }

        private void StartRoundTimer(int seconds)
        {
            _roundTimer?.Stop();
            TimeRemaining = seconds;
            IsTimeUp = false;

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

                    await NextCard();
                }
            };
            _roundTimer.Start();
        }



        private void StopRoundTimer()
        {
            _roundTimer?.Stop();
        }
    }
}
