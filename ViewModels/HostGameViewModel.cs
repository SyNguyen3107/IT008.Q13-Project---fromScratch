using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EasyFlips.ViewModels
{
    /// <summary>
    /// ViewModel quản lý màn hình Game phía Host (Người tổ chức).
    /// Chịu trách nhiệm: Điều khiển Timer, chuyển thẻ, tính điểm và Broadcast trạng thái cho Members.
    /// </summary>
    public partial class HostGameViewModel : BaseGameViewModel
    {
        private DispatcherTimer _roundTimer;

        #region Properties

        [ObservableProperty] private bool _isCardFlipped;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private string _statusColor = "#FF5E57"; // Màu đỏ nhạt mặc định
        [ObservableProperty] private string _currentQuestionInfo;
        [ObservableProperty] private int _progressMaximum;
        [ObservableProperty] private string _correctAnswer;

        #endregion

        public HostGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
            : base(authService, supabaseService, navigationService, audioService)
        {
        }

        #region Initialization

        /// <summary>
        /// Khởi tạo Game Host: Tải Deck, sắp xếp thẻ, lấy danh sách thành viên và bắt đầu đếm ngược.
        /// </summary>
        public override async Task InitializeAsync(
            string roomId,
            string classroomId,
            Deck deck,
            int timePerRound)
        {
            await base.InitializeAsync(roomId, classroomId, deck, timePerRound);

            if (deck == null || deck.Cards.Count == 0)
                throw new InvalidOperationException("Deck trống, không thể bắt đầu game");

            // 1. Subscribe kênh Realtime để nhận điểm số (Score) từ Member
            await SubscribeToRealtimeChannel();

            // Delay nhỏ để đảm bảo kết nối ổn định trước khi bắn tin đầu tiên
            await Task.Delay(150);

            // 2. [QUAN TRỌNG] Sắp xếp thẻ theo ID
            // Điều này bắt buộc để Member (dùng cùng logic sort) hiển thị đúng thẻ tương ứng với Host
            if (CurrentDeck != null && CurrentDeck.Cards != null)
            {
                CurrentDeck.Cards = CurrentDeck.Cards.OrderBy(c => c.Id).ToList();
            }

            // 3. Khởi tạo thẻ đầu tiên
            CurrentIndex = 0;
            CurrentCard = CurrentDeck.Cards.ElementAt(CurrentIndex);
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";

            // 4. Load danh sách thành viên hiện tại trong phòng
            var members = await _supabaseService.GetClassroomMembersWithProfileAsync(classroomId);
            UpdatePlayerList(members);

            // 5. Bắt đầu Countdown (3-2-1)
            StartCountdown();

            // Gửi tín hiệu bắt đầu phiên lên Server (Lưu DB)
            _ = _supabaseService.StartFlashcardSessionAsync(
                ClassroomId,
                _authService.CurrentUserId,
                deck.Id,
                CurrentCard.Id,
                deck.Cards.Count,
                timePerRound
            );
        }

        #endregion

        #region Timer Logic & State Machine

        /// <summary>
        /// Bắt đầu đếm ngược 3 giây trước khi vào câu hỏi.
        /// </summary>
        private void StartCountdown()
        {
            CurrentPhase = GamePhase.Waiting;
            StatusMessage = "Chuẩn bị bắt đầu";
            StatusColor = "#FF5E57"; // Red

            StartTimer(3);
        }

        /// <summary>
        /// Chuyển sang pha Câu Hỏi (Question Phase).
        /// Gửi tín hiệu NextCard cho Member.
        /// </summary>
        private async Task StartQuestionAsync()
        {
            CurrentPhase = GamePhase.Question;
            StatusMessage = "Trả lời câu hỏi";
            StatusColor = "#FF5E57";

            StartTimer(TotalTimePerRound);

            // Fire-and-forget broadcast: Gửi lệnh NextCard và lưu vào DB
            _ = BroadcastPhaseAsync(GamePhase.Question, FlashcardAction.NextCard);
        }

        /// <summary>
        /// Chuyển sang pha Kết Quả (Result Phase).
        /// Gửi tín hiệu FlipCard (lật mặt sau) cho Member.
        /// </summary>
        private async Task StartResultAsync()
        {
            CurrentPhase = GamePhase.Result;
            StatusMessage = "Xem kết quả";
            StatusColor = "#27AE60"; // Green

            StartTimer(10); // Thời gian xem kết quả (10s)

            _ = BroadcastPhaseAsync(GamePhase.Result, FlashcardAction.FlipCard);
        }

        /// <summary>
        /// Logic đếm ngược thời gian.
        /// </summary>
        /// <param name="seconds">Số giây đếm ngược</param>
        private void StartTimer(int seconds)
        {
            _roundTimer?.Stop();
            TimeRemaining = seconds;
            ProgressMaximum = seconds;

            _roundTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _roundTimer.Tick += (_, __) =>
            {
                TimeRemaining--;
                if (TimeRemaining <= 0)
                {
                    _roundTimer.Stop();
                    _ = HandleTimeUpAsync(); // Xử lý khi hết giờ
                }
            };
            _roundTimer.Start();
        }

        /// <summary>
        /// Xử lý chuyển trạng thái khi Timer về 0.
        /// </summary>
        private async Task HandleTimeUpAsync()
        {
            _roundTimer?.Stop();
            try
            {
                switch (CurrentPhase)
                {
                    case GamePhase.Waiting:
                        // Hết thời gian chờ -> Vào câu hỏi
                        await StartQuestionAsync();
                        break;

                    case GamePhase.Question:
                        // Hết thời gian trả lời -> Tự động lật thẻ -> Xem kết quả
                        await FlipCardAsync();
                        await StartResultAsync();
                        break;

                    case GamePhase.Result:
                        // Hết thời gian xem kết quả -> Sang câu tiếp theo
                        await GoToNextCardOrEndAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HandleTimeUp] Error: {ex.Message}");
            }
        }

        #endregion

        #region Game Flow Actions

        /// <summary>
        /// Lật thẻ (Hiển thị đáp án).
        /// </summary>
        [RelayCommand]
        public async Task FlipCardAsync()
        {
            ShowCard(); // Cập nhật UI Host

            CorrectAnswer = CurrentCard?.Answer;

            // Gọi Service để update trạng thái (Log purpose)
            _ = _supabaseService.FlipCardAsync(
                ClassroomId,
                _authService.CurrentUserId,
                CurrentDeck.Id,
                CurrentCard.Id,
                CurrentIndex,
                CurrentDeck.Cards.Count,
                TimeRemaining
            );
        }

        /// <summary>
        /// Chuyển sang thẻ tiếp theo hoặc kết thúc game nếu hết thẻ.
        /// </summary>
        private async Task GoToNextCardOrEndAsync()
        {
            // Nếu đã là thẻ cuối cùng -> Kết thúc
            if (CurrentIndex + 1 >= CurrentDeck.Cards.Count)
            {
                await EndGameAsync();
                return;
            }

            // Tăng Index và reset trạng thái
            CurrentIndex++;
            CurrentCard = CurrentDeck.Cards.ElementAt(CurrentIndex);
            CurrentQuestionInfo = $"{CurrentIndex + 1}/{CurrentDeck.Cards.Count}";
            IsCardFlipped = false;

            // Broadcast lệnh chuyển thẻ và bắt đầu Timer câu hỏi mới
            _ = BroadcastPhaseAsync(GamePhase.Question, FlashcardAction.NextCard);
            await StartQuestionAsync();
        }

        [RelayCommand] private void BackToFront() => IsCardFlipped = false;

        [RelayCommand]
        private void ShowCard()
        {
            IsCardFlipped = true;
            CorrectAnswer = CurrentCard?.Answer;
        }

        /// <summary>
        /// Kết thúc phiên chơi, lưu trạng thái EndSession và đóng cửa sổ.
        /// </summary>
        private async Task EndGameAsync()
        {
            await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            _ = BroadcastPhaseAsync(GamePhase.Finished, FlashcardAction.EndSession);
            CurrentPhase = GamePhase.Finished;

            ForceCloseWindow();
        }

        #endregion

        #region Broadcast & Realtime

        protected override async Task SubscribeToRealtimeChannel()
        {
            // Host lắng nghe để nhận điểm số từ Member
            await _supabaseService.SubscribeToFlashcardChannelAsync(
                ClassroomId,
                OnFlashcardStateReceived, // Host nhận lại state của mình (optional)
                OnScoreReceived           // Quan trọng: Nhận điểm số để update Leaderboard
            );
        }

        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            // Host chủ động điều khiển state nên hàm này chủ yếu để debug hoặc log
        }

        /// <summary>
        /// Xử lý khi nhận được bài làm từ Member.
        /// </summary>
        private void OnScoreReceived(ScoreSubmission submission)
        {
            // Tìm player trong danh sách hoặc thêm mới
            var player = Players.FirstOrDefault(p => p.Id == submission.UserId);
            if (player == null)
            {
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
        }

        /// <summary>
        /// Gửi trạng thái Game hiện tại lên Server (Lưu vào Database).
        /// Member sẽ lắng nghe thay đổi này để đồng bộ UI.
        /// </summary>
        private async Task BroadcastPhaseAsync(GamePhase phase, FlashcardAction action)
        {
            try
            {
                var state = new FlashcardSyncState
                {
                    ClassroomId = ClassroomId,
                    DeckId = CurrentDeck?.Id,
                    CurrentCardId = CurrentCard?.Id,
                    CurrentCardIndex = CurrentIndex,
                    TotalCards = CurrentDeck.Cards.Count,
                    Phase = phase,
                    Action = action,
                    IsFlipped = IsCardFlipped,
                    TimeRemaining = TimeRemaining,
                    TriggeredBy = _authService.CurrentUserId,
                    IsSessionActive = phase != GamePhase.Finished
                };

                // Gọi hàm lưu vào DB (Cơ chế Hybrid Sync)
                await _supabaseService.BroadcastFlashcardStateAsync(ClassroomId, state);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BroadcastPhase] Error: {ex.Message}");
            }
        }

        #endregion
    }
}