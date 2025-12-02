using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex.DiffBuilder.Model;
using EasyFlips.Models;
using EasyFlips.Services;
using System.Collections.ObjectModel; // Cần cho ObservableCollection
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class StudyViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private readonly AudioService _audioService;
        private int _currentDeckId; //Deck đang học
        private Card? _currentCard; // Thẻ đang học
        private readonly ComparisonService _comparisonService = new ComparisonService(); // Dịch vụ so sánh


        // --- Các thuộc tính hiển thị lên View ---
        [ObservableProperty] private string _questionText = string.Empty;
        [ObservableProperty] private string _answerText = string.Empty; // Đây là Explanation (Giải thích/Mặt sau)
        [ObservableProperty] private string _correctAnswer = string.Empty; // Đây là Keyword chuẩn (từ CSDL)

        [ObservableProperty] private string? _frontImagePath;
        [ObservableProperty] private string? _backImagePath;
        [ObservableProperty] private string? _frontAudioPath;
        [ObservableProperty] private string? _backAudioPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInputRequired))]
        private bool _isAnswerVisible = false;

        public bool IsInputRequired => !IsAnswerVisible; // Ví dụ: Ẩn input khi đã hiện đáp án

        [ObservableProperty]
        private string _userInputText = string.Empty;

        // Kiểm tra xem còn thẻ để học không (để enable/disable các nút)
        // Khởi tạo = true để các nút được enable ngay từ đầu
        [ObservableProperty]
        private bool _hasCards = true;
        // --- DANH SÁCH KẾT QUẢ SO SÁNH (Để binding lên View) ---
        public ObservableCollection<ComparisonChar> ComparisonResult { get; } = new ObservableCollection<ComparisonChar>();
        // --- KHAI BÁO COMMAND CHO 4 NÚT (bổ sung) ---
        public IAsyncRelayCommand AgainCommand { get; }
        public IAsyncRelayCommand HardCommand { get; }
        public IAsyncRelayCommand GoodCommand { get; }
        public IAsyncRelayCommand EasyCommand { get; }
        // Constructor nhận StudyService qua DI (không khởi tạo dữ liệu giả ở đây)
        public StudyViewModel(StudyService studyService, AudioService audioService)
        {
            _studyService = studyService;
            _audioService = audioService;
            // Khởi tạo command + điều kiện CanExecute dựa vào HasCards
            AgainCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Again), () => HasCards);
            HardCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Hard), () => HasCards);
            GoodCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Good), () => HasCards);
            EasyCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Easy), () => HasCards);
        }

        // Hàm khởi tạo ViewModel khi NavigationService gọi (hoặc khi ViewModel được tải)
        public async Task InitializeAsync(int deckId)
        {
            _currentDeckId = deckId; // 1. Lưu lại Deck ID
            await LoadNextCardAsync();   // 2. Tải thẻ đầu tiên
        }

        private async Task LoadNextCardAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[StudyViewModel] LoadNextCardAsync: Starting..");

                // Reset trạng thái
                IsAnswerVisible = false;
                UserInputText = string.Empty;
                ComparisonPieces.Clear();

                // Lấy thẻ
                _currentCard = await _studyService.GetNextCardToReviewAsync(_currentDeckId);

                System.Diagnostics.Debug.WriteLine($"[StudyViewModel] LoadNextCardAsync: Got card = {(_currentCard != null ? _currentCard.ID.ToString() : "null")}");

                // Cập nhật HasCards dựa trên _currentCard
                HasCards = _currentCard != null;
                System.Diagnostics.Debug.WriteLine($"[StudyViewModel] LoadNextCardAsync: HasCards = {HasCards}");

                if (_currentCard != null)
                {
                    QuestionText = _currentCard.FrontText ?? string.Empty;
                    AnswerText = _currentCard.BackText ?? string.Empty; // Mặt sau (Giải thích)

                    // Lấy đáp án chuẩn từ thuộc tính Answer mới thêm (nếu có), hoặc fallback về BackText
                    CorrectAnswer = _currentCard.Answer ?? "";

                    FrontImagePath = _currentCard.FrontImagePath;
                    BackImagePath = _currentCard.BackImagePath;
                    FrontAudioPath = _currentCard.FrontAudioPath;
                    BackAudioPath = _currentCard.BackAudioPath;

                    System.Diagnostics.Debug.WriteLine($"[StudyViewModel] LoadNextCardAsync: Card loaded successfully");
                }
                else
                {
                    // --- HẾT THẺ: HIỂN THỊ THÔNG BÁO ---
                    // Không tự động đóng cửa sổ, chỉ hiển thị thông báo
                    QuestionText = "Congratulations!";
                    AnswerText = "You have completed all the cards in this deck for now!\n\nPlease close this window.";
                    CorrectAnswer = "";

                    // Xóa các đường dẫn media
                    FrontImagePath = null;
                    BackImagePath = null;
                    FrontAudioPath = null;
                    BackAudioPath = null;

                    // Hiển thị phần answer để người dùng thấy thông báo
                    IsAnswerVisible = true;
                }

                // Thông báo cho 4 command rằng CanExecute đã đổi -> UI sẽ enable/disable
                AgainCommand.NotifyCanExecuteChanged();
                HardCommand.NotifyCanExecuteChanged();
                GoodCommand.NotifyCanExecuteChanged();
                EasyCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StudyViewModel] ERROR in LoadNextCardAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[StudyViewModel] Stack trace: {ex.StackTrace}");

                MessageBox.Show($"Error loading next card: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();

        // Hiện đáp án (người dùng nhấn để xem mặt sau)
        [RelayCommand]
        private void ShowAnswer()
        {
            IsAnswerVisible = true;
            // Tính điểm và tạo so sánh
            GenerateComparison();
        }




        private void GenerateComparison()
        {
            ComparisonPieces.Clear();

            var pieces = _comparisonService.GetCharDiff(UserInputText, CorrectAnswer);

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Input = '{UserInputText}'");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Target = '{CorrectAnswer}'");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Diff count = {pieces.Count}");

            if (pieces.Count == 0 && !string.IsNullOrEmpty(UserInputText))
            {
                ComparisonPieces.Add(new DiffPiece(UserInputText, ChangeType.Unchanged));
            }
            else
            {
                foreach (var piece in pieces)
                {
                    System.Diagnostics.Debug.WriteLine($"Piece: '{piece.Text}' Type: {piece.Type}");
                    ComparisonPieces.Add(piece);
                }

            }
        }



        // Các lệnh đánh giá: khi người dùng chọn Again/Hard/Good/Easy
        // Mỗi lệnh gọi StudyService.ProcessReviewAsync rồi tải thẻ kế tiếp

        // --- LỆNH PHÁT ÂM THANH ---
        [RelayCommand]
        private void PlayAudio(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Gọi AudioService để phát nhạc ngay trong ứng dụng
            _audioService.PlayAudio(path);
        }

        private async Task ProcessReview(ReviewOutcome outcome)
        {
            try
            {
                if (_currentCard == null) return; // Không có thẻ
                await _studyService.ProcessReviewAsync(_currentCard, outcome); // Cập nhật tiến trình thẻ
                await LoadNextCardAsync(); // Tải thẻ kế tiếp
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unidentified Error(s) occurred", "Error");
            }
        }

        public void StopAudio()
        {
            _audioService.StopAudio();
        }
    }
    // Class phụ để lưu thông tin hiển thị từng ký tự
    public class ComparisonChar
    {
        public string Character { get; set; }
        public string Color { get; set; } // Mã màu Hex (#RRGGBB)
        public string FontWeight { get; set; } = "Normal";
    }
    // Hàm này sẽ được gọi từ Code-behind khi cửa sổ đóng

}