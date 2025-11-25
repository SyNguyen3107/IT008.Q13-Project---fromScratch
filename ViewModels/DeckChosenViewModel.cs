using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Services;
using System.Threading.Tasks;
using System.Windows;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    // ViewModel cho cửa sổ DeckChosenWindow
    public partial class DeckChosenViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private readonly INavigationService _navigationService;

        // Lưu deckId để dùng khi người dùng nhấn Study Now
        private int _deckId;

        private string _deckName = "Loading...";

        private int _newCount;

        /// Số thẻ mới
        public String DeckName
        {
            get => _deckName;
            set => SetProperty(ref _deckName, value);
        }
        public int NewCount
        {
            get => _newCount;
            set => SetProperty(ref _newCount, value);
        }

        private int _learningCount;

        /// Số thẻ đang học
        public int LearningCount
        {
            get => _learningCount;
            set => SetProperty(ref _learningCount, value);
        }

        private int _reviewCount;

        /// Số thẻ ôn tập
        public int ReviewCount
        {
            get => _reviewCount;
            set => SetProperty(ref _reviewCount, value);
        }

        // Constructor: nhận StudyService và NavigationService từ DI
        public DeckChosenViewModel(StudyService studyService, INavigationService navigationService)
        {
            _studyService = studyService;
            _navigationService = navigationService;
        }

        // Hàm khởi tạo bất đồng bộ: lấy thống kê từ StudyService
        // Ghi chú: gọi từ NavigationService khi mở cửa sổ
        public async Task InitializeAsync(int deckId)
        {
            _deckId = deckId; // lưu lại deckId để dùng cho StudyNowCommand
            

            // Gọi service để lấy thông tin thống kê (New/Learning/Review)
            var stats = await _studyService.GetDeckStatsAsync(deckId);

            // Cập nhật thuộc tính (UI sẽ tự động cập nhật nhờ SetProperty)
            DeckName = stats.DeckName ?? "Unknown";
            NewCount = stats?.NewCount ?? 0;
            LearningCount = stats?.LearningCount ?? 0;
            ReviewCount = stats?.ReviewCount ?? 0;
        }

        // Lệnh mở cửa sổ học cho deck hiện tại
        [RelayCommand]
        private void StudyNow(Window window)
        {
            // 1. Kiểm tra xem có thẻ nào cần học không
            int totalCardsToStudy = NewCount + LearningCount + ReviewCount;

            if (totalCardsToStudy > 0)
            {
                // Có thẻ -> Mở cửa sổ học
                _navigationService.ShowStudyWindow(_deckId);

                // --- TỰ ĐỘNG ĐÓNG CỬA SỔ HIỆN TẠI ---
                if (window != null)
                {
                    window.Close();
                }
            }
            else
            {
                // Không có thẻ -> Chỉ hiện thông báo
                MessageBox.Show("You have already completed this deck!\nPlease comeback later.",
                                "Completed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
        }
    }
}
