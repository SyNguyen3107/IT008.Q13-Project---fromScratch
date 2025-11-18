using IT008.Q13_Project___fromScratch.ViewModels; // Thêm
using System.Windows;

namespace IT008.Q13_Project___fromScratch.Views
{
    public partial class StudyWindow : Window
    {
        // Giữ một tham chiếu (không bắt buộc nhưng nên có)
        private readonly StudyViewModel _viewModel;

        // Constructor nhận ViewModel từ DI
        public StudyWindow(StudyViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel; // <-- DÒNG QUAN TRỌNG NHẤT
        }
    }
}