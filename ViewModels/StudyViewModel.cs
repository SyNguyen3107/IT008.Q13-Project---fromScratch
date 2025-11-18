using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System.Threading.Tasks; // 👈 Đảm bảo có thư viện này
using System.Windows.Input;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class StudyViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private int _currentDeckId; // 👈 Biến để lưu Deck ID
        private Card _currentCard;

        // ... (Các thuộc tính ObservableProperty của bạn) ...
        [ObservableProperty]
        private string _questionText;
        // ...

        [ObservableProperty]
        private bool _isAnswerVisible = false;

        // Constructor
        public StudyViewModel(StudyService studyService)
        {
            _studyService = studyService;
            // Không tải dữ liệu ở đây!
        }

        // === HÀM BẠN CẦN THÊM ===
        // (Hàm này sẽ được NavigationService gọi)
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
                QuestionText = _currentCard.FrontText;
                // ... (cập nhật các thuộc tính khác)
            }
            else
            {
                QuestionText = "Bạn đã hoàn thành bộ thẻ này!";
                // ... (xử lý khi học hết)
            }
        }

        // ... (Các RelayCommand của bạn) ...
    }
}