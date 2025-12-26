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
        private bool _isShowingResult;
        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private string _connectionStatus = "Chưa kết nối";

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
            Debug.WriteLine($"[MemberGame] 🚀 InitializeAsync started");
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (deck == null)
            {
                Debug.WriteLine($"[MemberGame] 📦 Deck is null, fetching from cloud...");
                deck = await _supabaseService.GetDeckByClassroomIdAsync(classroomId);
                if (deck == null || deck.Cards == null || deck.Cards.Count == 0)
                {
                    MessageBox.Show("Deck trống, không thể tham gia game.");
                    return;
                }
            }

            CurrentDeck = deck;
            Debug.WriteLine($"[MemberGame] ✅ Deck loaded: {deck.Name} with {deck.Cards.Count} cards");

            // ✅ QUAN TRỌNG: Hiển thị card đầu tiên ngay lập tức
            if (deck.Cards.Any())
            {
                CurrentCard = deck.Cards.First();
                CurrentIndex = 0;
                TotalCards = deck.Cards.Count;
                IsInputEnabled = true; // Cho phép Member nhập đáp án
                CurrentPhase = GamePhase.Question;
                TimeRemaining = timePerRound;
                
                Debug.WriteLine($"[MemberGame] 🎴 First card set: {CurrentCard.FrontText}");
                Debug.WriteLine($"[MemberGame] ✅ Member ready to play!");
            }

            // Các thuộc tính như _roomId, _classroomId đã được gán tự động ở lớp cha
            await SubscribeToRealtimeChannel();
        }

        /// <summary>
        /// Implement phương thức abstract từ BaseGameViewModel
        /// ✅ Sử dụng Postgres Changes thay vì Broadcast (vì broadcast bị lỗi payload null)
        /// </summary>
        protected override async Task SubscribeToRealtimeChannel()
        {
            try
            {
                // Kiểm tra ClassroomId
                if (string.IsNullOrEmpty(ClassroomId))
                {
                    Debug.WriteLine($"[MemberGame] ❌ ClassroomId is null or empty!");
                    ConnectionStatus = "Lỗi: ClassroomId trống";
                    return;
                }

                Debug.WriteLine($"[MemberGame] 🔄 Đang subscribe Postgres Changes: {ClassroomId}");
                
                // Update UI ngay để user biết đang kết nối
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStatus = "Đang kết nối...";
                });
                
                // ✅ Sử dụng Postgres Changes thay vì Broadcast
                var success = await _supabaseService.SubscribeToGameStateChangesAsync(
                    ClassroomId,
                    OnFlashcardStateReceived
                );

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        Debug.WriteLine($"[MemberGame] ✅ Đã kết nối Postgres Changes!");
                        ConnectionStatus = "🟢 Đã kết nối (Real-time)";
                    }
                    else
                    {
                        Debug.WriteLine($"[MemberGame] ❌ Kết nối thất bại");
                        ConnectionStatus = "❌ Kết nối thất bại";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemberGame] ❌ Lỗi khi subscribe: {ex.Message}");
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStatus = $"Lỗi: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// XỬ LÝ TASK: Cập nhật UI Card và Timer khi nhận gói tin từ Host
        /// </summary>
        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Log rõ ràng khi Member nhận được message
                System.Diagnostics.Debug.WriteLine("==========================================");
                System.Diagnostics.Debug.WriteLine($"[Member] 📩 ĐÃ NHẬN ĐƯỢC MESSAGE TỪ HOST!");
                System.Diagnostics.Debug.WriteLine($"[Member] Phase={state.Phase}, Action={state.Action}");
                System.Diagnostics.Debug.WriteLine($"[Member] CardIndex={state.CurrentCardIndex}, CardId={state.CurrentCardId}");
                System.Diagnostics.Debug.WriteLine($"[Member] TimeRemaining={state.TimeRemaining}");
                System.Diagnostics.Debug.WriteLine("==========================================");
                
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
                    Score += 10;
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
                    Score,
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