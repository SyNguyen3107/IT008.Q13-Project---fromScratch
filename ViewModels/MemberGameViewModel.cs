using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace EasyFlips.ViewModels
{
    public partial class MemberGameViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;
        private readonly ComparisonService _comparisonService;
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;

        private string _roomId = string.Empty;
        private string _classroomId = string.Empty;
        private Deck? _deck;
        private int _timePerRound;

        // Binding IsInputEnable
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))]
        private bool isInputEnabled;

        [ObservableProperty]
        private string userAnswer = string.Empty;

        [ObservableProperty]
        private FlashcardSyncState? currentState;

        private bool _submittedThisCard = false;
        private string _correctAnswer = string.Empty;

        public MemberGameViewModel(SupabaseService supabaseService, ComparisonService comparisonService, IAuthService authService)
        {
            _supabaseService = supabaseService;
            _comparisonService = comparisonService;
            _authService = authService;
            IsInputEnabled = false;
        }

        /// <summary>
        /// Khởi tạo Member Game - Subscribe vào kênh Realtime.
        /// </summary>
        public async Task InitializeAsync(string roomId, string classroomId, Deck? deck, int timePerRound)
        {
            _roomId = roomId;
            _classroomId = classroomId;
            _deck = deck;
            _timePerRound = timePerRound;

            // Subscribe vào kênh flashcard sync
            var result = await _supabaseService.SubscribeToFlashcardChannelAsync(
                classroomId,
                OnFlashcardStateReceived
            );

            if (result.Success)
            {
                Debug.WriteLine($"[MemberGame] Subscribed to channel: {result.ChannelName}");
            }
            else
            {
                Debug.WriteLine($"[MemberGame] Subscribe failed: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Callback khi nhận được trạng thái mới từ Host.
        /// </summary>
        private void OnFlashcardStateReceived(FlashcardSyncState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentState = state;
                Debug.WriteLine($"[MemberGame] Received: {state.Action} - Card {state.CurrentCardIndex + 1}/{state.TotalCards}");

                switch (state.Action)
                {
                    case FlashcardAction.ShowCard:
                    case FlashcardAction.StartSession:
                        OnQuestionReceived(state);
                        break;

                    case FlashcardAction.FlipCard:
                        // Nếu chưa submit thì auto-submit đáp án đang gõ dở
                        if (!_submittedThisCard && IsInputEnabled)
                        {
                            SubmitAnswer();
                        }
                        IsInputEnabled = false;
                        break;

                    case FlashcardAction.NextCard:
                        OnQuestionReceived(state);
                        break;

                    case FlashcardAction.EndSession:
                        IsInputEnabled = false;
                        Debug.WriteLine("[MemberGame] Session ended");
                        _navigationService.ShowLeaderBoardWindow();
                        break;
                }
            });
        }

        // Gọi hàm này khi nhận tín hiệu "Question" từ Server/Host
        public void OnQuestionReceived(FlashcardSyncState? state = null)
        {
            UserAnswer = "";
            IsInputEnabled = true;
            _submittedThisCard = false;
            // Lấy đáp án đúng từ _deck.Cards (dùng BackText/Answer)
            if (state != null && _deck != null && state.CurrentCardIndex < _deck.Cards.Count)
            {
                _correctAnswer = ((List<Card>)_deck.Cards)[state.CurrentCardIndex].Answer;

            }
            else
            {
                _correctAnswer = string.Empty;
            }
        }
        // Hàm xử lý nộp bài(Dùng cho cả Nút Submit và Phím Enter)
        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async void SubmitAnswer()
        {
            if (_submittedThisCard || !IsInputEnabled) return;
            _submittedThisCard = true;

            // So sánh đáp án
            bool isCorrect = _comparisonService.IsAnswerAcceptable(UserAnswer, _correctAnswer);
            int deltaScore = isCorrect ? 10 : 0; // Ví dụ: đúng +10

            // Gửi điểm lên Host
            var submission = new ScoreSubmission
            {
                UserId = _authService.CurrentUserId,
                DisplayName = "", // Có thể lấy từ profile nếu cần
                CardIndex = CurrentState?.CurrentCardIndex ?? 0,
                Answer = UserAnswer,
                IsCorrect = isCorrect,
                Score = deltaScore,
                TimeTakenMs = 0, // Có thể tính thời gian nếu muốn
                SubmittedAt = DateTime.UtcNow
            };
            await _supabaseService.BroadcastScoreSubmissionAsync(_classroomId, submission);

            // Sau khi nộp xong thì KHÓA lại ngay
            IsInputEnabled = false;
        }
        // Điều kiện để được phép nộp (Chỉ nộp được khi IsInputEnabled = true)
        private bool CanSubmit()
        {
            return IsInputEnabled;
        }

 


    }
}
