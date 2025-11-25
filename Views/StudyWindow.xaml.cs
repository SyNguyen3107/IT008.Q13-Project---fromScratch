using EasyFlips.ViewModels; // Thêm
using System.Windows;

namespace EasyFlips.Views
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
        // --- XỬ LÝ KHI ĐÓNG CỬA SỔ ---
        protected override void OnClosed(EventArgs e)
        {
            // Gọi ViewModel để dừng mọi âm thanh đang phát
            _viewModel.StopAudio();

            // Gọi xử lý đóng cửa sổ mặc định
            base.OnClosed(e);
        }
    }
}