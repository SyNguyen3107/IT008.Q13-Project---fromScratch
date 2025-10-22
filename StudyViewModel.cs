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

        

        // Command "ShowAnswer"
  
    }
}