using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Models;
using EasyFlips.Services;
using EasyFlips.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;

namespace EasyFlips.ViewModels
{
    /// <summary>
    /// Các giai đoạn của vòng lặp Game
    /// </summary>
    public enum GamePhase
    {
        Waiting,        // Chờ bắt đầu
        Question,       // Hiện câu hỏi (15s)
        Result,         // Hiện kết quả (10s)
        Finished        // Kết thúc
    }

    /// <summary>
    /// ViewModel cha chứa logic chung cho cả Host và Member
    /// </summary>
    public abstract partial class BaseGameViewModel : ObservableObject
    {
        #region Services
        protected readonly IAuthService _authService;
        protected readonly SupabaseService _supabaseService;
        protected readonly INavigationService _navigationService;
        protected readonly AudioService _audioService;
        #endregion

        #region Shared Properties

        [ObservableProperty] private string _roomId;
        [ObservableProperty] private string _classroomId;

        [ObservableProperty] private Deck _currentDeck;
        [ObservableProperty] private Card _currentCard;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressText))]
        private int _currentIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressText))]
        private int _totalCards;

        public string ProgressText => $"{CurrentIndex + 1}/{TotalCards}";

        // Timer & State
        [ObservableProperty] private int _timeRemaining;
        [ObservableProperty] private int _totalTimePerRound;
        [ObservableProperty] private GamePhase _currentPhase = GamePhase.Waiting;

        // Điểm số (Member dùng để hiện điểm mình, Host có thể không dùng hoặc dùng để hiện cái gì đó khác)
        [ObservableProperty] private int _currentScore;

        // Helper cho UI biết đang ở phase nào để hiện mặt sau
        public bool IsShowingResult => CurrentPhase == GamePhase.Result;
        #endregion

        public BaseGameViewModel(
            IAuthService authService,
            SupabaseService supabaseService,
            INavigationService navigationService,
            AudioService audioService)
        {
            _authService = authService;
            _supabaseService = supabaseService;
            _navigationService = navigationService;
            _audioService = audioService;
        }

        /// <summary>
        /// Hàm khởi tạo chung, được gọi sau khi Constructor
        /// </summary>
        public virtual async Task InitializeAsync(string roomId, string classroomId, Deck deck, int timePerRound)
        {
            RoomId = roomId;
            ClassroomId = classroomId;
            CurrentDeck = deck;
            TotalTimePerRound = timePerRound;

            if (CurrentDeck != null && CurrentDeck.Cards != null)
            {
                TotalCards = CurrentDeck.Cards.Count;
            }

            await SubscribeToRealtimeChannel();
        }

        /// <summary>
        /// Phương thức abstract bắt buộc lớp con phải định nghĩa logic Realtime riêng
        /// </summary>
        protected abstract Task SubscribeToRealtimeChannel();

        [RelayCommand]
        public virtual async Task QuitGame()
        {
            var message = "Bạn có chắc muốn thoát game? Hành động này không thể hoàn tác.";
            // Logic kiểm tra Host nằm ở lớp con, hoặc check đơn giản ở đây

            if (MessageBox.Show(message, "Xác nhận thoát", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Logic dọn dẹp riêng của lớp con (Host hủy phòng, Member rời phòng)
                    await OnQuitSpecificAsync();

                    // 2. Rời kênh Realtime chung
                    await _supabaseService.LeaveFlashcardSyncChannelAsync(ClassroomId);

                    // 3. Về trang chủ
                    _navigationService.ShowMainWindow();

                    // 4. Đóng cửa sổ hiện tại
                    ForceCloseWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thoát: {ex.Message}");
                    ForceCloseWindow();
                }
            }
        }

        /// <summary>
        /// Logic thoát game đặc thù (Host thì EndSession, Member thì chỉ Leave)
        /// </summary>
        protected virtual Task OnQuitSpecificAsync() => Task.CompletedTask;

        protected void ForceCloseWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    // Đóng cửa sổ đang giữ ViewModel này
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            });
        }
    }
}