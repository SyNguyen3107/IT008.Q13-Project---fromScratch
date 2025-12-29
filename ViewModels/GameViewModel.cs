using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex.DiffBuilder.Model;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyFlips.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly IClassroomRepository _classroomRepository;
        private readonly ComparisonService _comparisonService = new ComparisonService();

        [ObservableProperty] private int _score;
        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _classroomId;
        [ObservableProperty] private Deck _selectedDeck;
        [ObservableProperty] private int _maxPlayers;
        [ObservableProperty] private int _timePerRound;
        [ObservableProperty] private int _totalTimePerRound;

        [ObservableProperty] private Card _currentCard;
        [ObservableProperty] private string _userAnswer;
        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _resultMessage;

        [ObservableProperty] private bool _isHost;
        [ObservableProperty] private string _currentQuestionInfo; [ObservableProperty] private bool _isAnswerSubmitted;
        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        private DispatcherTimer _roundTimer;
        private int _remainingSeconds;

        public GameViewModel(IAuthService authService,
                             SupabaseService supabaseService,
                             IClassroomRepository classroomRepository)
        {
            _authService = authService;
            _supabaseService = supabaseService;
            _classroomRepository = classroomRepository;
        }

        public async Task InitializeAsync(string roomId, string classroomId, Deck deck, int maxPlayers, int timePerRound)
        {
            RoomId = roomId;
            ClassroomId = classroomId;
            SelectedDeck = deck;
            MaxPlayers = maxPlayers;
            TimePerRound = timePerRound;
            TotalTimePerRound = timePerRound;

            var currentUserId = _authService.CurrentUserId;

            await LoadPlayers();

            var me = Players.FirstOrDefault(p => p.Id == currentUserId);
            IsHost = me?.IsHost ?? false;

            await _supabaseService.JoinFlashcardSyncChannelAsync(ClassroomId, currentUserId, OnGameStateReceived);

            if (IsHost && SelectedDeck.Cards.Any())
            {
                await _supabaseService.StartFlashcardSessionAsync(
    ClassroomId,
    currentUserId,
    SelectedDeck.Id,
    SelectedDeck.Cards.First().Id,
    SelectedDeck.Cards.Count,
    TotalTimePerRound);
            }
        }

        private async Task LoadPlayers()
        {
            var members = await _supabaseService.GetClassroomMembersWithProfileAsync(ClassroomId);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Players.Clear();
                foreach (var m in members)
                {
                    Players.Add(new PlayerInfo
                    {
                        Id = m.UserId,
                        Name = m.DisplayName ?? "Unknown",
                        AvatarUrl = !string.IsNullOrEmpty(m.AvatarUrl) ? m.AvatarUrl : "/Images/default_user.png",
                        IsHost = (m.Role == "owner" || m.Role == "host"),
                        Score = 0
                    });
                }
            });
        }


        private void OnGameStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                HandleGameSyncState(state);
            });
        }

        private void HandleGameSyncState(FlashcardSyncState state)
        {
            if (SelectedDeck != null && state.CurrentCardIndex < SelectedDeck.Cards.Count)
            {
                var targetCard = SelectedDeck.Cards.ElementAt(state.CurrentCardIndex);
                if (CurrentCard?.Id != targetCard.Id)
                {
                    CurrentCard = targetCard;
                    UserAnswer = string.Empty;
                    IsAnswerSubmitted = false;
                    ResultMessage = string.Empty;
                    ComparisonPieces.Clear();
                }
            }

            CurrentQuestionInfo = $"{state.CurrentCardIndex + 1}/{state.TotalCards}";

            switch (state.Action)
            {
                case FlashcardAction.StartSession:
                case FlashcardAction.NextCard:
                case FlashcardAction.ShowCard:
                    IsCardFlipped = false;
                    StartRoundTimer(state.TimeRemaining); break;

                case FlashcardAction.FlipCard:
                    IsCardFlipped = true;
                    StopRoundTimer();

                    if (IsAnswerSubmitted)
                    {
                        GenerateComparison();
                    }
                    break;

                case FlashcardAction.EndSession:
                    MessageBox.Show("The session has ended!", "Notification");
                    break;
            }
        }


        private void StartRoundTimer(int seconds)
        {
            _roundTimer?.Stop();
            _remainingSeconds = seconds;
            TimePerRound = _remainingSeconds;

            _roundTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _roundTimer.Tick += (s, e) =>
            {
                if (_remainingSeconds > 0)
                {
                    _remainingSeconds--;
                    TimePerRound = _remainingSeconds;
                }
                else
                {
                    _roundTimer.Stop();
                }
            };
            _roundTimer.Start();
        }

        private void StopRoundTimer()
        {
            _roundTimer?.Stop();
        }


        [ObservableProperty] private string _answerText;
        [ObservableProperty] private string _correctAnswer;
        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();

        [RelayCommand]
        private void SubmitAnswer()
        {
            if (CurrentCard == null || IsAnswerSubmitted) return;

            IsAnswerSubmitted = true;
            ResultMessage = "Submitted! Waiting for the host to flip the card...";

            AnswerText = UserAnswer;
            CorrectAnswer = CurrentCard.Answer;

        }

        private void GenerateComparison()
        {
            ComparisonPieces.Clear();
            if (string.IsNullOrEmpty(CorrectAnswer)) return;

            var score = _comparisonService.SmartScore(UserAnswer ?? "", CorrectAnswer);

            if (score >= 80)
            {
                Score += 10;
                ResultMessage = "That's right! You earned 10 points.";
                UpdatePlayerScore(_authService.CurrentUserId, Score);
            }
            else
            {
                ResultMessage = "Not quite right.";
            }

            var pieces = score < 50
            ? _comparisonService.GetWordDiff(UserAnswer ?? "", CorrectAnswer)
            : _comparisonService.GetCharDiff(UserAnswer ?? "", CorrectAnswer);

            if (pieces.Count == 0 && !string.IsNullOrEmpty(UserAnswer))
                ComparisonPieces.Add(new DiffPiece(UserAnswer, ChangeType.Unchanged));
            else
                foreach (var piece in pieces) ComparisonPieces.Add(piece);
        }


        [RelayCommand]
        private async Task NextCard()
        {
            if (!IsHost || SelectedDeck == null || CurrentCard == null) return;

            var cardsList = SelectedDeck.Cards.ToList();
            var currentIndex = cardsList.IndexOf(CurrentCard);
            var nextIndex = currentIndex + 1;

            if (nextIndex < cardsList.Count)
            {
                var nextCard = cardsList[nextIndex];

                await _supabaseService.NextCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                SelectedDeck.Id,
                nextCard.Id,
                nextIndex,
                cardsList.Count,
                TotalTimePerRound
                );
            }
            else
            {
                await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            }
        }

        [RelayCommand]
        private async Task FlipCard()
        {
            if (!IsHost || CurrentCard == null) return;

            var cardsList = SelectedDeck.Cards.ToList();
            var currentIndex = cardsList.IndexOf(CurrentCard);

            await _supabaseService.FlipCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                SelectedDeck.Id,
                CurrentCard.Id,
                currentIndex,
                cardsList.Count,
                _remainingSeconds
            );
        }


        [RelayCommand]
        private async Task QuitGame()
        {
            var result = MessageBox.Show(
             IsHost ? "You are the host. Quitting will disband the room. Continue?" : "Are you sure you want to quit the game?",
             "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _roundTimer?.Stop();
                    await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);

                    if (IsHost)
                    {
                        await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
                    }

                    CloseCurrentWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error");
                }
            }
        }

        private void CloseCurrentWindow()
        {
            if (Application.Current == null) return;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                    window?.Close();
                });
            }
            catch { }
        }
        public void UpdatePlayerScore(string userId, int newScore)
        {
            var player = Players.FirstOrDefault(p => p.Id == userId);
            if (player != null)
            {
                player.Score = newScore;

                SortPlayersDescending();
            }
        }
        private void SortPlayersDescending()
        {
            var sortedList = Players.OrderByDescending(p => p.Score).ToList();

            for (int i = 0; i < sortedList.Count; i++)
            {
                var item = sortedList[i];
                int oldIndex = Players.IndexOf(item);

                if (oldIndex != i)
                {
                    Players.Move(oldIndex, i);
                }
            }
        }
    }
}