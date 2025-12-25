using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EasyFlips.ViewModels
{
    /// <summary>
    /// ViewModel dành riêng cho Member - Kế thừa BaseGameViewModel của Dev A
    /// </summary>
    public partial class MemberGameViewModel : BaseGameViewModel
    {
        private readonly ComparisonService _comparisonService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private bool _isInputEnabled;

        [ObservableProperty]
        private string _userAnswer = string.Empty;

        [ObservableProperty]
        private string _resultMessage = string.Empty;

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

        /// <summary>
        /// Ghi đè hàm Initialize từ lớp cha để đăng ký Realtime
        /// </summary>
        public override async Task InitializeAsync(string roomId, string classroomId, Deck? deck, int timePerRound)
        {
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);
            // Các thuộc tính như _roomId, _classroomId đã được gán tự động ở lớp cha (base.InitializeAsync)
        }

        /// <summary>
        /// Implement phương thức abstract từ BaseGameViewModel
        /// </summary>
        protected override async Task SubscribeToRealtimeChannel()
        {
            var result = await _supabaseService.SubscribeToFlashcardChannelAsync(
                ClassroomId,
                OnFlashcardStateReceived
            );

            if (result.Success)
                Debug.WriteLine($"[MemberGame] Đã kết nối kênh: {result.ChannelName}");
            else
                Debug.WriteLine($"[MemberGame] Kết nối thất bại: {result.ErrorMessage}");
        }

        /// <summary>
        /// XỬ LÝ TASK: Cập nhật UI Card và Timer khi nhận gói tin từ Host
        /// </summary>
        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. Đồng bộ Index và Card
                if (CurrentDeck != null && (CurrentCard == null || CurrentCard.Id != state.CurrentCardId))
                {
                    var newCard = CurrentDeck.Cards.FirstOrDefault(c => c.Id == state.CurrentCardId);
                    if (newCard != null)
                    {
                        CurrentCard = newCard;
                        CurrentIndex = state.CurrentCardIndex;
                    }
                }

                // 2. Đồng bộ Timer từ Host
                TimeRemaining = state.TimeRemaining;

                // 3. Cập nhật Phase (Trạng thái game) dựa trên Action
                UpdatePhaseFromAction(state.Action);
            });
        }

        private void UpdatePhaseFromAction(FlashcardAction action)
        {
            switch (action)
            {
                case FlashcardAction.ShowCard:
                case FlashcardAction.StartSession:
                case FlashcardAction.NextCard:
                    // TASK: Reset TextBox khi sang câu mới
                    PrepareForNewQuestion();
                    break;

                case FlashcardAction.FlipCard:
                    // TASK: Khóa TextBox khi Host lật mặt sau
                    HandleFlipCard();
                    break;

                case FlashcardAction.EndSession:
                    CurrentPhase = GamePhase.Finished;
                    IsInputEnabled = false;
                    MessageBox.Show("Phiên học đã kết thúc!");
                    break;
            }
        }

        /// <summary>
        /// XỬ LÝ TASK: Reset TextBox và mở khóa nhập liệu
        /// </summary>
        private void PrepareForNewQuestion()
        {
            CurrentPhase = GamePhase.Question;
            UserAnswer = string.Empty; // Reset TextBox
            ResultMessage = string.Empty;
            IsInputEnabled = true;

            // Thông báo UI cập nhật IsShowingResult (để ẩn mặt sau)
            OnPropertyChanged(nameof(IsShowingResult));
        }

        private void HandleFlipCard()
        {
            CurrentPhase = GamePhase.Result;

            // Nếu Member chưa nộp bài mà Host đã lật thẻ (hết giờ), tự động nộp ngay
            if (IsInputEnabled)
            {
                SubmitAnswerCommand.Execute(null);
            }

            IsInputEnabled = false;
            OnPropertyChanged(nameof(IsShowingResult)); // Hiện mặt sau
        }

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task SubmitAnswer()
        {
            if (CurrentCard != null)
            {
                // 1. Dùng IsAnswerAcceptable để chấm điểm
                bool isCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, CurrentCard.BackText);

                if (isCorrect)
                {
                    CurrentScore += 10;
                    ResultMessage = "Chính xác! +10đ";
                }
                else
                {
                    ResultMessage = $"Sai rồi! Đáp án là: {CurrentCard.BackText}";
                }

                // 2. Gọi ĐÚNG hàm mà Dev D đã chuẩn bị trong SupabaseService
                // Hàm này nhận vào: classroomId, userId, score, số câu đúng, tổng số câu đã trả lời
                await _supabaseService.SendFlashcardScoreAsync(
                    ClassroomId,
                    _authService.CurrentUserId,
                    CurrentScore,
                    isCorrect ? 1 : 0,
                    1
                );
            }

            // Khóa UI sau khi nộp
            IsInputEnabled = false;
        }

        private bool CanSubmit() => IsInputEnabled;
    }
}