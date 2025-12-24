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

        // --- Properties ---
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

        // Trạng thái để quản lý UI
        [ObservableProperty] private bool _isHost;
        [ObservableProperty] private string _currentQuestionInfo; // Ví dụ: "Question 1/10"
        [ObservableProperty] private bool _isAnswerSubmitted; // Để khóa ô nhập liệu sau khi submit

        public ObservableCollection<PlayerInfo> Players { get; } = new ObservableCollection<PlayerInfo>();

        // Timer
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

            // Xác định quyền Host
            var currentUserId = _authService.CurrentUserId;
            // (Logic đơn giản: Nếu user hiện tại là chủ phòng trong DB hoặc người tạo ra phòng này)
            // Tạm thời ta check dựa trên list member hoặc logic truyền vào. 
            // Ở đây tôi giả định logic check Host đã đúng từ bên ngoài truyền vào hoặc check sau.

            // Load danh sách người chơi ban đầu
            await LoadPlayers();

            // Check xem mình có phải Host trong list players không
            var me = Players.FirstOrDefault(p => p.Id == currentUserId);
            IsHost = me?.IsHost ?? false;

            // [REALTIME] Kết nối vào kênh đồng bộ Game
            await _supabaseService.JoinFlashcardSyncChannelAsync(ClassroomId, currentUserId, OnGameStateReceived);

            // Nếu là Host và chưa bắt đầu -> Gửi tín hiệu bắt đầu game (Start Session)
            if (IsHost && SelectedDeck.Cards.Any())
            {
                // Host chủ động Start Session -> Gửi tín hiệu ShowCard đầu tiên
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

        // --- REALTIME HANDLER ---

        /// <summary>
        /// Hàm này chạy mỗi khi nhận được tín hiệu từ Server (do Host bấm Next/Flip...)
        /// </summary>
        private void OnGameStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                HandleGameSyncState(state);
            });
        }

        private void HandleGameSyncState(FlashcardSyncState state)
        {
            // 1. Cập nhật thẻ hiện tại dựa trên Index từ Server
            if (SelectedDeck != null && state.CurrentCardIndex < SelectedDeck.Cards.Count)
            {
                var targetCard = SelectedDeck.Cards.ElementAt(state.CurrentCardIndex);
                // Chỉ cập nhật nếu khác thẻ cũ để tránh reload ảnh
                if (CurrentCard?.Id != targetCard.Id)
                {
                    CurrentCard = targetCard;
                    // Reset trạng thái cho thẻ mới
                    UserAnswer = string.Empty;
                    IsAnswerSubmitted = false;
                    ResultMessage = string.Empty;
                    ComparisonPieces.Clear();
                }
            }

            // 2. Cập nhật thông tin hiển thị (Ví dụ: Question 2/10)
            CurrentQuestionInfo = $"{state.CurrentCardIndex + 1}/{state.TotalCards}";

            // 3. Xử lý hành động cụ thể
            switch (state.Action)
            {
                case FlashcardAction.StartSession:
                case FlashcardAction.NextCard:
                case FlashcardAction.ShowCard:
                    // Host vừa bấm Next hoặc Start -> Reset Timer, Mặt trước
                    IsCardFlipped = false;
                    StartRoundTimer(state.TimeRemaining); // Đồng bộ thời gian với Server
                    break;

                case FlashcardAction.FlipCard:
                    // Host vừa bấm Flip (Show Answer) -> Lật thẻ, hiện kết quả
                    IsCardFlipped = true;
                    StopRoundTimer();

                    // Nếu người dùng đã nộp bài, lúc này mới hiện chấm điểm chi tiết
                    if (IsAnswerSubmitted)
                    {
                        GenerateComparison();
                    }
                    break;

                case FlashcardAction.EndSession:
                    MessageBox.Show("Buổi học đã kết thúc!", "Thông báo");
                    // Có thể điều hướng về Lobby hoặc hiện bảng tổng sắp
                    break;
            }
        }

        // --- TIMER LOGIC ---

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
                    // Khi hết giờ cục bộ, ta chờ tín hiệu từ Host
                    // Hoặc nếu là Host thì tự động gửi lệnh Flip/Next (sẽ làm ở Phase sau)
                }
            };
            _roundTimer.Start();
        }

        private void StopRoundTimer()
        {
            _roundTimer?.Stop();
        }

        // --- STUDENT ACTIONS ---

        [ObservableProperty] private string _answerText;
        [ObservableProperty] private string _correctAnswer;
        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();

        [RelayCommand]
        private void SubmitAnswer()
        {
            // Student bấm Submit
            if (CurrentCard == null || IsAnswerSubmitted) return;

            // Đánh dấu đã nộp
            IsAnswerSubmitted = true;
            ResultMessage = "Đã nộp bài! Chờ Host lật thẻ...";

            // Logic tính điểm NGẦM (sẽ gửi lên server ở Phase 2)
            // Tạm thời chưa hiện ComparisonPieces ngay để tạo kịch tính
            // Ta chỉ tính điểm để đó
            AnswerText = UserAnswer;
            CorrectAnswer = CurrentCard.Answer;

            // Tạm thời tính điểm cục bộ luôn để test (nhưng chưa hiện diff)
            // Score += ... (Phase 2 sẽ gửi update score)
        }

        private void GenerateComparison()
        {
            // Hàm này được gọi khi Host bấm FLIP CARD
            ComparisonPieces.Clear();
            if (string.IsNullOrEmpty(CorrectAnswer)) return;

            // Logic so sánh cũ của bạn
            var score = _comparisonService.SmartScore(UserAnswer ?? "", CorrectAnswer);

            if (score >= 80) // Ví dụ: Đúng trên 80% thì cộng điểm
            {
                Score += 10; // Cộng điểm cục bộ (Hiển thị vui)
                ResultMessage = "Chính xác! (+10 điểm)";
                UpdatePlayerScore(_authService.CurrentUserId, Score);
            }
            else
            {
                ResultMessage = "Chưa chính xác.";
            }

            // Hiện Diff
            var pieces = score < 50
                ? _comparisonService.GetWordDiff(UserAnswer ?? "", CorrectAnswer)
                : _comparisonService.GetCharDiff(UserAnswer ?? "", CorrectAnswer);

            if (pieces.Count == 0 && !string.IsNullOrEmpty(UserAnswer))
                ComparisonPieces.Add(new DiffPiece(UserAnswer, ChangeType.Unchanged));
            else
                foreach (var piece in pieces) ComparisonPieces.Add(piece);
        }

        // --- HOST ACTIONS (Chỉ Host mới dùng được các lệnh này) ---

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

                // Gửi tín hiệu NEXT_CARD lên Server
                // Mọi người (bao gồm cả Host) sẽ nhận tín hiệu này ở HandleGameSyncState và cập nhật UI
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
                // Hết thẻ -> End Session
                await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
            }
        }

        [RelayCommand]
        private async Task FlipCard()
        {
            // Host bấm nút "Show Answer"
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

        // --- SYSTEM ACTIONS ---

        [RelayCommand]
        private async Task QuitGame()
        {
            var result = MessageBox.Show(
                IsHost ? "Bạn là Host. Thoát game sẽ giải tán phòng?" : "Bạn muốn thoát game?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _roundTimer?.Stop();
                    // Rời kênh realtime
                    await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);

                    if (IsHost)
                    {
                        // Nếu Host thoát -> End Session cho mọi người
                        await _supabaseService.EndFlashcardSessionAsync(ClassroomId, _authService.CurrentUserId);
                    }

                    CloseCurrentWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi: {ex.Message}");
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
        // --  LOGIC SẮP XẾP BẢNG XẾP HẠNG --
        /// <summary>
        /// Cập nhật điểm cho một người chơi và tự động sắp xếp lại danh sách
        /// </summary>
        /// 
        public void UpdatePlayerScore(string userId, int newScore)
        {
            // Tìm người chơi trong danh sách
            var player = Players.FirstOrDefault(p => p.Id == userId);
            if (player != null)
            {
                // Cập nhật điểm
                player.Score = newScore;

                // Gọi hàm sắp xếp (sẽ kích hoạt Animation bên View)
                SortPlayersDescending();
            }
        }
        /// <summary>
        /// Sắp xếp danh sách Players từ điểm cao xuống thấp
        /// Sử dụng lệnh Move() để kích hoạt hiệu ứng trượt trên giao diện
        /// </summary>
        private void SortPlayersDescending()
        {
            // 1. Tạo bản sao danh sách đã sắp xếp đúng
            var sortedList = Players.OrderByDescending(p => p.Score).ToList();

            // 2. So sánh và di chuyển các phần tử sai vị trí
            for (int i = 0; i < sortedList.Count; i++)
            {
                var item = sortedList[i];
                int oldIndex = Players.IndexOf(item);

                // Nếu vị trí hiện tại không đúng với vị trí sau khi sắp xếp
                if (oldIndex != i)
                {
                    // Di chuyển item về đúng vị trí
                    // Lệnh Move này sẽ được FluidMoveBehavior bên View bắt được và tạo hiệu ứng trượt
                    Players.Move(oldIndex, i);
                }
            }
        }
    }
}