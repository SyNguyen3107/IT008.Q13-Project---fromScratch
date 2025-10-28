using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System.Security;
using System.Threading.Tasks; // 👈 Đảm bảo có thư viện này
using System.Windows.Input;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class StudyViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private int _currentDeckId; // 👈 Biến để lưu Deck ID
        private Card? _currentCard; // Thẻ hiện tại đang hiển thị

        // Thuộc tính binding cho giao diện (sử dụng source-generator của CommunityToolkit)
        [ObservableProperty]
        private string _questionText = string.Empty;

        [ObservableProperty]
        private string _answerText = string.Empty;

        [ObservableProperty]
        private bool _isAnswerVisible = false;

        // Constructor nhận StudyService qua DI (không khởi tạo dữ liệu giả ở đây)
        public StudyViewModel(StudyService studyService)
        {
            _studyService = studyService;
            // Không tải dữ liệu ở đây!
        }

        // Hàm khởi tạo ViewModel khi NavigationService gọi (hoặc khi ViewModel được tải)
        // Lưu deckId và tải thẻ đầu tiên
        public async Task InitializeAsync(int deckId)
        {
            _currentDeckId = deckId; // 1. Lưu lại Deck ID
            await LoadNextCardAsync();   // 2. Tải thẻ đầu tiên
        }

        private async Task LoadNextCardAsync()
        {
            IsAnswerVisible = false; // Luôn reset về mặt trước
            _currentCard = await _studyService.GetNextCardToReviewAsync(_currentDeckId);

            if (_currentCard != null)
            {
                // Cập nhật các thuộc tính, View sẽ tự động thay đổi
                QuestionText = _currentCard.FrontText ?? string.Empty;
                AnswerText = _currentCard.BackText ?? string.Empty;
            }
            else
            {
                // Nếu không còn thẻ nào đến hạn
                QuestionText = "Bạn đã hoàn thành bộ thẻ này!";
                AnswerText = string.Empty;
            }
        }

        // Hiện đáp án (người dùng nhấn để xem mặt sau)
        [RelayCommand]
        private void ShowAnswer()
        {
            IsAnswerVisible = true;
        }

        // Các lệnh đánh giá: khi người dùng chọn Again/Hard/Good/Easy
        // Mỗi lệnh gọi StudyService.ProcessReviewAsync rồi tải thẻ kế tiếp
        [RelayCommand]
        private async Task AgainAsync()
        {
            await ProcessOutcomeAndLoadNextAsync(ReviewOutcome.Again);
        }

        [RelayCommand]
        private async Task HardAsync()
        {
            await ProcessOutcomeAndLoadNextAsync(ReviewOutcome.Hard);
        }

        [RelayCommand]
        private async Task GoodAsync()
        {
            await ProcessOutcomeAndLoadNextAsync(ReviewOutcome.Good);
        }

        [RelayCommand]
        private async Task EasyAsync()
        {
            await ProcessOutcomeAndLoadNextAsync(ReviewOutcome.Easy);
        }

        // Hàm helper xử lý outcome: gọi service cập nhật, sau đó tải thẻ tiếp theo
        private async Task ProcessOutcomeAndLoadNextAsync(ReviewOutcome outcome)
        {
            if (_currentCard == null)
            {
                // Không có thẻ để xử lý — có thể log hoặc hiển thị thông báo nếu cần
                return;
            }

            // Gọi service để xử lý kết quả ôn tập (cập nhật Interval, EaseFactor, DueDate và lưu)
            await _studyService.ProcessReviewAsync(_currentCard, outcome);

            // Tải thẻ tiếp theo sau khi đã lưu kết quả ôn tập
            await LoadNextCardAsync();
        }
    }
}