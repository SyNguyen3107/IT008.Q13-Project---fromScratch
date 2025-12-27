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

        // [QUAN TRỌNG] Khi biến này đổi -> Báo lệnh Submit kiểm tra lại
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private bool _isInputEnabled;

        [ObservableProperty]
        private bool _isShowingResult;

        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private string _connectionStatus = "Đang kết nối...";

        // [QUAN TRỌNG] Khi gõ chữ -> Báo lệnh Submit kiểm tra lại ngay lập tức
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private string _userAnswer = string.Empty;

        [ObservableProperty]
        private string _resultMessage = string.Empty;

        [ObservableProperty]
        private string _currentQuestionInfo;

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
        }

        public override async Task InitializeAsync(string roomId, string classroomId, Deck? deck, int timePerRound)
        {
            if (deck == null) deck = await _supabaseService.GetDeckByClassroomIdAsync(classroomId);
            if (deck != null && deck.Cards != null) deck.Cards = deck.Cards.OrderBy(c => c.Id).ToList();

            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (CurrentDeck != null && CurrentDeck.Cards.Count > 0)
            {
                CurrentIndex = 0;
                CurrentCard = CurrentDeck.Cards.ElementAt(0);
                CurrentQuestionInfo = $"1/{CurrentDeck.Cards.Count}";
            }

            await SubscribeToRealtimeChannel();

            // Load lại trạng thái cũ nếu lỡ vào sau
            var currentClassroom = await _supabaseService.GetClassroomAsync(classroomId);
            if (currentClassroom?.SyncState != null) OnFlashcardStateReceived(currentClassroom.SyncState);
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
                // CHỈ return nếu CẢ index VÀ action đều y hệt cái cũ (tránh xử lý trùng tin nhắn realtime)
                // Nhưng hãy cẩn thận: Nếu Server gửi lại cùng 1 Action để Reset, ta vẫn nên nhận.
                if (state.CurrentCardIndex == _lastProcessedIndex && state.Action == _lastProcessedAction)
                    return;

                // Cập nhật vết (track) dữ liệu cuối cùng đã xử lý
                _lastProcessedIndex = state.CurrentCardIndex;
                _lastProcessedAction = state.Action;

                if (CurrentDeck == null || CurrentDeck.Cards == null) return;

                // Cập nhật Index và Card hiện tại
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

        private void UpdatePhaseFromAction(FlashcardAction action)
        {
            switch (action)
            {
                case FlashcardAction.StartSession:
                case FlashcardAction.NextCard:
                    PrepareForNewQuestion();
                    break;
                case FlashcardAction.FlipCard:
                    HandleFlipCard();
                    break;
                case FlashcardAction.EndSession:
                    IsInputEnabled = false;
                    MessageBox.Show("Kết thúc phiên!", "Thông báo");
                    break;
            }
        }

        private void PrepareForNewQuestion()
        {
            Debug.WriteLine($"[GameFlow] Đang chuẩn bị câu hỏi mới (Index: {CurrentIndex})");

            CurrentPhase = GamePhase.Question;
            UserAnswer = "";
            ResultMessage = string.Empty;
            IsShowingResult = false;

            // Mở khóa input
            IsInputEnabled = true;
            Debug.WriteLine($"[GameFlow] IsInputEnabled đã set thành: {IsInputEnabled}");

            SubmitAnswerCommand.NotifyCanExecuteChanged();
        }
        private void HandleFlipCard()
        {
            CurrentPhase = GamePhase.Result;

            if (CanSubmit())
            {
                SubmitAnswerCommand.Execute(null);
            }

            IsInputEnabled = false;
            SubmitAnswerCommand.NotifyCanExecuteChanged();
        }


        // [QUAN TRỌNG] Gắn hàm kiểm tra điều kiện vào Command
        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAnswer()
        {
            

            IsInputEnabled = false;
          

            if (CurrentCard != null)
            {
                bool isCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard.Answer);
                if (isCorrect)
                {
                    Score += 10;
                    ResultMessage = "Chính xác! +10đ";
                }
                else
                {
                    ResultMessage = "Sai rồi!";
                }

                _= _supabaseService.SendFlashcardScoreAsync(
                    ClassroomId, _authService.CurrentUserId, Score, isCorrect ? 1 : 0, 1
                );
            }
        }
        partial void OnIsInputEnabledChanged(bool value)
{
    // Ép lệnh Submit cập nhật trạng thái ngay lập tức trên UI
        SubmitAnswerCommand.NotifyCanExecuteChanged();
}
        partial void OnUserAnswerChanged(string value)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SubmitAnswerCommand.NotifyCanExecuteChanged();
            });
        }

        // Hàm này quyết định nút Sáng hay Tối
        private bool CanSubmit()
        {
            return IsInputEnabled && !string.IsNullOrWhiteSpace(UserAnswer);
        }
    }
}