using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex.DiffBuilder.Model;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberGameViewModel : BaseGameViewModel
    {
        private int _lastProcessedIndex = -1;
        private FlashcardAction _lastProcessedAction = FlashcardAction.None;
        private System.Timers.Timer? _countdownTimer;
        [ObservableProperty]
        private int _startCountdownValue;
        private bool _hasStartedFirstCountdown = false;
        [ObservableProperty]
        private bool _isShowingStartCountdown;

        private int _localCorrectCount = 0;
        private int _localTotalAnswered = 0;

        private int _pendingScoreEarned = 0;
        private string _pendingResultMessage = string.Empty;
        private bool _pendingIsCorrect = false;
        [ObservableProperty]
        private bool _isCorrect = false;

        // [THÊM] Cờ kiểm soát trạng thái nộp bài
        private bool _hasSubmitted = false;

        private readonly ComparisonService _comparisonService = new ComparisonService();
        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();
        private string _myDisplayName = "Unknown Player";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private bool _isInputEnabled;

        [ObservableProperty]
        private bool _isShowingResult;

        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private string _connectionStatus = "Connecting...";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private string _userAnswer = string.Empty;
        private int time;

        [ObservableProperty]
        private string _resultMessage = string.Empty;

        [ObservableProperty]
        private string _currentQuestionInfo;

        [ObservableProperty]
        private string _correctAnswer = string.Empty;

        public MemberGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService,
            ComparisonService comparisonService)
            : base(authService, supabaseService, navigationService, audioService)
        {
            _comparisonService = comparisonService;
            IsInputEnabled = false;
            QuitConfirmationMessage = "Do you want to leave the room?";
        }

        public override async Task InitializeAsync(string roomId, string classroomId, Deck? deck, int timePerRound)
        {
            try
            {
                if (deck == null) deck = await _supabaseService.GetDeckByClassroomIdAsync(classroomId);
                if (deck != null && deck.Cards != null) deck.Cards = deck.Cards.OrderBy(c => c.Id).ToList();

                var userId = _authService.CurrentUserId;
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await _supabaseService.GetProfileAsync(userId);
                    if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                    {
                        _myDisplayName = profile.DisplayName;
                    }
                    else
                    {
                        var email = _supabaseService.Client.Auth.CurrentUser?.Email;
                        if (!string.IsNullOrEmpty(email))
                        {
                            _myDisplayName = email.Split('@')[0];
                        }
                        else
                        {
                            _myDisplayName = $"Player {userId.Substring(0, 4)}";
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[MemberVM] Init Profile Error: {ex.Message}");
            }

            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (CurrentDeck != null && CurrentDeck.Cards.Count > 0)
            {
                CurrentIndex = 0;
                CurrentCard = CurrentDeck.Cards.ElementAt(0);
                CurrentQuestionInfo = $"1/{CurrentDeck.Cards.Count}";
            }
            time = timePerRound;
            await SubscribeToRealtimeChannel();

            var currentClassroom = await _supabaseService.GetClassroomAsync(classroomId);
            if (currentClassroom?.SyncState != null) OnFlashcardStateReceived(currentClassroom.SyncState);
            SetupTimer();
        }

        private void SetupTimer()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();

            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += (s, e) =>
            {
                if (TimeRemaining > 0)
                {
                    TimeRemaining--;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => IsInputEnabled = false);
                    _countdownTimer.Stop();
                }
            };
            _countdownTimer.Start();
        }

        private async Task RunInitialCountdown(int seconds)
        {
            IsShowingStartCountdown = true;
            IsInputEnabled = false;
            StartCountdownValue = seconds;

            while (StartCountdownValue > 0)
            {
                await Task.Delay(1000);
                StartCountdownValue--;
            }

            IsShowingStartCountdown = false;
        }

        protected override async Task SubscribeToRealtimeChannel()
        {
            var result = await _supabaseService.SubscribeToFlashcardChannelAsync(ClassroomId, OnFlashcardStateReceived);
            ConnectionStatus = result.Success ? "Connected" : "Connection Failed";
        }

        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (state.CurrentCardIndex == _lastProcessedIndex && state.Action == _lastProcessedAction)
                    return;

                _lastProcessedIndex = state.CurrentCardIndex;
                _lastProcessedAction = state.Action;

                if (CurrentDeck == null || CurrentDeck.Cards == null) return;

                if (state.CurrentCardIndex >= 0 && state.CurrentCardIndex < CurrentDeck.Cards.Count)
                {
                    CurrentIndex = state.CurrentCardIndex;
                    CurrentCard = CurrentDeck.Cards.ElementAt(state.CurrentCardIndex);
                    CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";
                }

                TimeRemaining = state.TimeRemaining;
                UpdatePhaseFromAction(state.Action);
            });
        }

        private async void UpdatePhaseFromAction(FlashcardAction action)
        {
            switch (action)
            {
                case FlashcardAction.StartSession:
                    if (!_hasStartedFirstCountdown)
                    {
                        _hasStartedFirstCountdown = true;
                        await RunInitialCountdown(3);
                    }
                    PrepareForNewQuestion();
                    break;
                case FlashcardAction.NextCard:
                    PrepareForNewQuestion();
                    break;
                case FlashcardAction.FlipCard:
                    HandleFlipCard();
                    break;
                case FlashcardAction.EndSession:
                    IsInputEnabled = false;
                    DisposeTimer();
                    await NavigateToLeaderboardAsync();
                    break;
            }
        }

        private void PrepareForNewQuestion()
        {
            StopAudio();
            CurrentPhase = GamePhase.Question;
            _hasSubmitted = false; // [RESET CỜ] Reset trạng thái nộp bài cho câu mới
            UserAnswer = "";
            ResultMessage = string.Empty;
            IsShowingResult = false;
            IsInputEnabled = true;
            TimeRemaining = time;
            SetupTimer();
            SubmitAnswerCommand.NotifyCanExecuteChanged();
        }

        private async void HandleFlipCard()
        {
            CurrentPhase = GamePhase.Result;
            IsShowingResult = true;

            // --- [LOGIC AUTO-SUBMIT] ---
            if (!_hasSubmitted)
            {
                // Trường hợp 1: Hết giờ hoặc chưa bấm Submit -> Tự động chấm điểm
                _pendingIsCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard?.Answer ?? "");
                _pendingScoreEarned = _pendingIsCorrect ? 10 : 0;

                if (_pendingIsCorrect) _pendingResultMessage = "Excellent! +10 Points";
                else _pendingResultMessage = string.IsNullOrWhiteSpace(UserAnswer) ? "Timeout!" : "Incorrect!";

                // Cập nhật thống kê local
                _localTotalAnswered++;
                if (_pendingIsCorrect) _localCorrectCount++;

                // Cộng điểm local (Hiển thị ngay cho user thấy)
                Score += _pendingScoreEarned;

                // [QUAN TRỌNG] GỬI ĐIỂM LÊN SERVER ĐỂ LEADERBOARD THẤY
                await _supabaseService.SendFlashcardScoreAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    _myDisplayName,
                    Score, // Gửi điểm tổng mới nhất
                    _localCorrectCount,
                    _localTotalAnswered
                );

                _hasSubmitted = true; // Đánh dấu để không gửi lại
            }
            else
            {
                // Trường hợp 2: Đã bấm Submit trước đó -> Chỉ cần đồng bộ hiển thị
                // Vì hàm SubmitAnswer bên dưới đã gửi điểm lên Server rồi, nên ở đây KHÔNG gửi nữa để tránh Spam/Dup.

                // Tuy nhiên, ta cần đảm bảo Score hiển thị local khớp với Server.
                // Hàm SubmitAnswer mới (bên dưới) ĐÃ cộng điểm rồi, nên ở đây KHÔNG cần cộng nữa.
                // Nếu cộng tiếp Score += _pendingScoreEarned ở đây sẽ bị gấp đôi điểm.

                // -> Để an toàn, ta chỉ set lại các thông báo kết quả.
            }
            // ---------------------------

            ResultMessage = _pendingResultMessage;
            IsCorrect = _pendingIsCorrect;
            CorrectAnswer = CurrentCard?.Answer ?? "";
            GenerateComparison();
            TimeRemaining = 10;
            SetupTimer();
            IsInputEnabled = false;
            SubmitAnswerCommand.NotifyCanExecuteChanged();
            _pendingScoreEarned = 0;
        }

        private void GenerateComparison()
        {
            ComparisonPieces.Clear();
            if (string.IsNullOrEmpty(CorrectAnswer)) return;

            int score = _comparisonService.SmartScore(UserAnswer, CorrectAnswer);

            System.Collections.Generic.List<DiffPiece> pieces;
            if (score < 50)
            {
                pieces = _comparisonService.GetWordDiff(UserAnswer, CorrectAnswer);
            }
            else
            {
                pieces = _comparisonService.GetCharDiff(UserAnswer, CorrectAnswer);
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

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAnswer()
        {
            if (_hasSubmitted) return; // Chặn nếu đã nộp rồi
            _hasSubmitted = true;      // Đánh dấu đã nộp ngay lập tức

            IsInputEnabled = false;

            if (CurrentCard != null)
            {
                _pendingIsCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard.Answer);
                _pendingScoreEarned = _pendingIsCorrect ? 10 : 0;

                if (_pendingIsCorrect) _pendingResultMessage = "Excellent! +10 Points";
                else _pendingResultMessage = "Incorrect!";
                
                IsCorrect = _pendingIsCorrect;

                _localTotalAnswered++;
                if (_pendingIsCorrect) _localCorrectCount++;

                // [FIX] Cập nhật điểm hiển thị Local ngay lập tức
                Score += _pendingScoreEarned;

                ResultMessage = "Handed in! Calculating results...";

                // Gửi điểm tổng hợp lên Server
                await _supabaseService.SendFlashcardScoreAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    _myDisplayName,
                    Score, // Gửi điểm Score đã được cộng
                    _localCorrectCount,
                    _localTotalAnswered
                );
            }
        }

        partial void OnIsInputEnabledChanged(bool value)
        {
            SubmitAnswerCommand.NotifyCanExecuteChanged();
        }

        partial void OnUserAnswerChanged(string value)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SubmitAnswerCommand.NotifyCanExecuteChanged();
            });
        }

        private bool CanSubmit()
        {
            return IsInputEnabled && !string.IsNullOrWhiteSpace(UserAnswer);
        }

        public void DisposeTimer()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
        }

        [RelayCommand]
        private async Task WindowClosing(CancelEventArgs e)
        {
            if (_isGameEnded || _isQuitting)
            {
                StopAudio();
                return;
            }

            e.Cancel = true;
            await QuitGame();
        }

        protected override async Task OnQuitSpecificAsync()
        {
            try
            {
                await Task.CompletedTask;
            }
            catch { }
        }

        protected override async Task NavigateToLeaderboardAsync()
        {
            StopAudio();
            _isGameEnded = true;
            _navigationService.ShowMemberLeaderboardWindow(RoomId, ClassroomId);
            await Task.CompletedTask;
        }
    }
}