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
using static System.Formats.Asn1.AsnWriter;

namespace EasyFlips.ViewModels
{
    public partial class GameWindowViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly SupabaseService _supabaseService;
        private readonly IClassroomRepository _classroomRepository;
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

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        private DispatcherTimer _roundTimer;
        private int _remainingSeconds;

        public GameWindowViewModel(IAuthService authService,
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


            // Load danh sách người chơi từ Supabase
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

            // Lấy card đầu tiên
            if (SelectedDeck != null && SelectedDeck.Cards.Any())
            {
                CurrentCard = SelectedDeck.Cards.First();
            }

            StartRoundTimer();
        }

      
        private void StartRoundTimer()
        {
            _roundTimer?.Stop();

            _remainingSeconds = TotalTimePerRound;   
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
                    EndRound();
                }
            };
            _roundTimer.Start();
        }



        private readonly ComparisonService _comparisonService = new ComparisonService();

        [RelayCommand]
        private void SubmitAnswer()
        {
            if (CurrentCard == null) return;

            AnswerText = UserAnswer;
            CorrectAnswer = CurrentCard.Answer;

            GenerateComparison();

            var me = Players.FirstOrDefault(p => p.Id == (_authService.CurrentUserId));
            if (me != null && ResultMessage.Contains("Similarity Score"))
            {
                me.Score += 1;
                Score += 1;
            }

            IsCardFlipped = true;
        }

        [ObservableProperty] private string _answerText;
        [ObservableProperty] private string _correctAnswer;
        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();

        private void GenerateComparison()
        {
            ComparisonPieces.Clear();
            if (string.IsNullOrEmpty(CorrectAnswer)) return;

            Score = _comparisonService.SmartScore(UserAnswer ?? "", CorrectAnswer);

            List<DiffPiece> pieces;
            if (Score < 50)
            {
                pieces = _comparisonService.GetWordDiff(UserAnswer ?? "", CorrectAnswer);
            }
            else
            {
                pieces = _comparisonService.GetCharDiff(UserAnswer ?? "", CorrectAnswer);
            }

            if (pieces.Count == 0 && !string.IsNullOrEmpty(UserAnswer))
            {
                ComparisonPieces.Add(new DiffPiece(UserAnswer, ChangeType.Unchanged));
            }
            else
            {
                foreach (var piece in pieces) ComparisonPieces.Add(piece);
            }
        }


        [RelayCommand]
        private void NextCard()
        {
            if (SelectedDeck?.Cards == null || CurrentCard == null) return;

            var cardsList = SelectedDeck.Cards.ToList(); 
            var nextIndex = cardsList.IndexOf(CurrentCard) + 1;

            if (nextIndex < cardsList.Count)
            {
                CurrentCard = cardsList[nextIndex];
                UserAnswer = string.Empty;
                IsCardFlipped = false;
                ResultMessage = string.Empty;
                StartRoundTimer(); 
            }
            else
            {
                MessageBox.Show("Game Over!");
                IsCardFlipped = true;
                ResultMessage = "You've completed the deck!";
            }
        }

     
        private void EndRound()
        {
            ResultMessage = "Time's up!";
            IsCardFlipped = true;
        }
    }
}
