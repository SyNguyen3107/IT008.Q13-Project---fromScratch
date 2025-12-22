using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberGameViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;

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

        public MemberGameViewModel(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
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
            // Chạy trên UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentState = state;
                Debug.WriteLine($"[MemberGame] Received: {state.Action} - Card {state.CurrentCardIndex + 1}/{state.TotalCards}");

                switch (state.Action)
                {
                    case FlashcardAction.ShowCard:
                    case FlashcardAction.StartSession:
                        OnQuestionReceived();
                        break;

                    case FlashcardAction.FlipCard:
                        IsInputEnabled = false;
                        break;

                    case FlashcardAction.NextCard:
                        OnQuestionReceived();
                        break;

                    case FlashcardAction.EndSession:
                        IsInputEnabled = false;
                        Debug.WriteLine("[MemberGame] Session ended");
                        break;
                }
            });
        }

        // Gọi hàm này khi nhận tín hiệu "Question" từ Server/Host
        public void OnQuestionReceived()
        {
            UserAnswer = "";
            IsInputEnabled = true;
        }
        // Hàm xử lý nộp bài(Dùng cho cả Nút Submit và Phím Enter)
        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private void SubmitAnswer()
        {
            // Xử lý logic nộp bài ở đây
            MessageBox.Show($"Đã nộp đáp án: {UserAnswer}");

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
