using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input; // Cần thêm dòng này

namespace fromScratch_project // Đảm bảo namespace này đúng
{
    // Kế thừa từ ObservableObject
    public partial class StudyViewModel : ObservableObject
    {
        // Các thuộc tính
        [ObservableProperty]
        private string _questionText;

        [ObservableProperty]
        private string _answerText;

        [ObservableProperty]
        private string _frontImagePath;

        [ObservableProperty]
        private bool _isAnswerVisible;

        // Hàm khởi tạo
        public StudyViewModel()
        {
            LoadMockCard();
        }

        private void LoadMockCard()
        {
            QuestionText = "Câu hỏi giả (mock data)";
            AnswerText = "Trả lời giả";
            // TODO: Thay bằng đường dẫn ảnh thật trên máy bạn để test
            FrontImagePath = @"";
            IsAnswerVisible = false;
        }

        // Command "ShowAnswer"
        [RelayCommand]
        private void ShowAnswer()
        {
            IsAnswerVisible = true;
        }

        // Command "ProcessReview"
        [RelayCommand]
        private void ProcessReview(string result)
        {
            LoadMockCard(); // Nạp lại thẻ giả
        }
    }
}