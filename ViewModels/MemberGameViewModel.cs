using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberGameViewModel : BaseGameViewModel
    {
        private readonly ComparisonService _comparisonService;
        private int _lastProcessedIndex = -1;
        private FlashcardAction _lastProcessedAction = FlashcardAction.None;
        private System.Timers.Timer? _countdownTimer;
        [ObservableProperty]
        private int _startCountdownValue;
        private bool _hasStartedFirstCountdown = false;
        [ObservableProperty]
        private bool _isShowingStartCountdown;

        // [LOGIC MỚI] Biến theo dõi tổng số liệu cục bộ để Upsert
        private int _localCorrectCount = 0;
        private int _localTotalAnswered = 0;

        private int _pendingScoreEarned = 0;
        private string _pendingResultMessage = string.Empty;
        private bool _pendingIsCorrect = false;

        // [FIX LỖI IAuthService] Lưu tên hiển thị tại đây thay vì gọi AuthService liên tục
        private string _myDisplayName = "Unknown Player";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private bool _isInputEnabled;

        [ObservableProperty]
        private bool _isShowingResult;

        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private string _connectionStatus = "Đang kết nối...";

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

                // [FIX LỖI] Logic lấy DisplayName an toàn (Không phụ thuộc IAuthService thiếu field)
                var userId = _authService.CurrentUserId;
                if (!string.IsNullOrEmpty(userId))
                {
                    // 1. Ưu tiên: Lấy từ bảng Profile
                    var profile = await _supabaseService.GetProfileAsync(userId);
                    if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                    {
                        _myDisplayName = profile.DisplayName;
                    }
                    else
                    {
                        // 2. Dự phòng: Lấy từ Supabase Client trực tiếp (Bỏ qua IAuthService)
                        var email = _supabaseService.Client.Auth.CurrentUser?.Email;
                        if (!string.IsNullOrEmpty(email))
                        {
                            _myDisplayName = email.Split('@')[0];
                        }
                        else
                        {
                            // 3. Đường cùng
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

            // Load lại trạng thái cũ nếu lỡ vào sau
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
            IsInputEnabled = false; // Khóa không cho gõ khi đang đếm 3.2.1
            StartCountdownValue = seconds;

            while (StartCountdownValue > 0)
            {
                // _audioService.PlaySound("beep.mp3"); // Nếu bạn muốn thêm âm thanh
                await Task.Delay(1000);
                StartCountdownValue--;
            }

            IsShowingStartCountdown = false;
        }

        protected override async Task SubscribeToRealtimeChannel()
        {
            var result = await _supabaseService.SubscribeToFlashcardChannelAsync(ClassroomId, OnFlashcardStateReceived);
            ConnectionStatus = result.Success ? "Đã kết nối" : "Lỗi kết nối";
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
                    DisposeTimer(); // Dừng timer ngay lập tức

                    // [MỚI] Thông báo và điều hướng về MainWindow
                    MessageBox.Show("The host has ended this session.", "Notification");

                    // Thực hiện dọn dẹp giống như khi chủ động Quit
                    await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);
                    _navigationService.ShowMainWindow();
                    RequestCloseWindow(); // Gọi Action để View đóng cửa sổ
                    break;
            }
        }

        private void PrepareForNewQuestion()
        {
            CurrentPhase = GamePhase.Question;
            UserAnswer = "";
            ResultMessage = string.Empty;
            IsShowingResult = false;
            IsInputEnabled = true;
            TimeRemaining = time;
            SetupTimer();
            SubmitAnswerCommand.NotifyCanExecuteChanged();
        }

        private void HandleFlipCard()
        {
            CurrentPhase = GamePhase.Result;
            IsShowingResult = true;

            // Nếu người dùng chưa kịp bấm nút Submit mà đã hết giờ/lật thẻ
            if (IsInputEnabled)
            {
                // Tính toán nhanh kết quả mà không cần gọi qua Command để tránh delay UI
                _pendingIsCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard?.Answer ?? "");
                _pendingScoreEarned = _pendingIsCorrect ? 10 : 0;

                if (_pendingIsCorrect) _pendingResultMessage = "Chính xác! +10đ";
                else _pendingResultMessage = string.IsNullOrWhiteSpace(UserAnswer)
                    ? $"Hết giờ!"
                    : $"Sai rồi!";

                // Cập nhật điểm và số liệu
                Score += _pendingScoreEarned;
                _localTotalAnswered++;
                if (_pendingIsCorrect) _localCorrectCount++;
            }
            else
            {
                // Nếu đã Submit trước đó rồi, bây giờ chỉ việc cộng điểm vào UI
                Score += _pendingScoreEarned;
            }

            // HIỂN THỊ KẾT QUẢ CUỐI CÙNG LÊN UI
            ResultMessage = _pendingResultMessage;
            CorrectAnswer = CurrentCard?.Answer ?? "";

            // Reset các trạng thái cho vòng sau
            TimeRemaining = 10;
            SetupTimer();
            IsInputEnabled = false;
            SubmitAnswerCommand.NotifyCanExecuteChanged();
            _pendingScoreEarned = 0;
        }

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAnswer()
        {
            IsInputEnabled = false;

            if (CurrentCard != null)
            {
                // Tính toán kết quả NGẦM
                _pendingIsCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard.Answer);
                _pendingScoreEarned = _pendingIsCorrect ? 10 : 0;

                // Chuẩn bị thông báo (nhưng chưa hiện)
                if (_pendingIsCorrect) _pendingResultMessage = "Chính xác! +10đ";
                else _pendingResultMessage = $"Sai rồi!";

                // Cập nhật số liệu cục bộ
                _localTotalAnswered++;
                if (_pendingIsCorrect) _localCorrectCount++;

                // [QUAN TRỌNG] Tính tổng điểm DỰ KIẾN để gửi Server, nhưng CHƯA cộng vào UI Score
                int projectedTotalScore = Score + _pendingScoreEarned;

                // [UX] Hiện thông báo chờ
                ResultMessage = "Đã nộp bài! Chờ kết quả...";

                // Gửi điểm lên Server ngay để Host cập nhật Realtime (Host sẽ thấy điểm tăng ngay)
                // Nếu bạn muốn Host cũng chưa thấy điểm tăng thì gửi 'Score' cũ, 
                // nhưng thường Host cần thấy điểm ngay.
                await _supabaseService.SendFlashcardScoreAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    _myDisplayName,
                    projectedTotalScore, // Gửi điểm đã cộng
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
        protected override async Task OnQuitSpecificAsync()
        {
            try
            {
                // Logic rời phòng (nếu cần)
                await Task.CompletedTask;
            }
            catch { }
        }
    }
}