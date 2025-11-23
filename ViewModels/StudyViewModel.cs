using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Services;
using System.Collections.ObjectModel; // Cần cho ObservableCollection
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Linq;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class StudyViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private readonly AudioService _audioService;
        private int _currentDeckId; //Deck đang học
        private Card? _currentCard; // Thẻ đang học

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

        // --- DANH SÁCH KẾT QUẢ SO SÁNH (Để binding lên View) ---
        public ObservableCollection<ComparisonChar> ComparisonResult { get; } = new ObservableCollection<ComparisonChar>();
        // Constructor nhận StudyService qua DI (không khởi tạo dữ liệu giả ở đây)
        public StudyViewModel(StudyService studyService, AudioService audioService)
        {
            _studyService = studyService;
            _audioService = audioService;
        }

        // Hàm khởi tạo ViewModel khi NavigationService gọi (hoặc khi ViewModel được tải)
        public async Task InitializeAsync(int deckId)
        {
            _currentDeckId = deckId; // 1. Lưu lại Deck ID
            await LoadNextCardAsync();   // 2. Tải thẻ đầu tiên
        }

        private async Task LoadNextCardAsync()
        {
            // Reset trạng thái
            IsAnswerVisible = false;
            UserInputText = string.Empty;
            ComparisonResult.Clear(); // Xóa kết quả cũ

            // Lấy thẻ
            _currentCard = await _studyService.GetNextCardToReviewAsync(_currentDeckId);

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
            }
            else
            {
                // --- HẾT THẺ: THÔNG BÁO VÀ ĐÓNG CỬA SỔ ---
                MessageBox.Show("Congratulation! You have completed this deck!",
                                "Completed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                // Tìm cửa sổ đang chứa ViewModel này và đóng nó
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.DataContext == this)
                        {
                            window.Close();
                            break;
                        }
                    }
                });
            }

        }

        // Hiện đáp án (người dùng nhấn để xem mặt sau)
        [RelayCommand]
        private void ShowAnswer()
        {
            IsAnswerVisible = true;
            GenerateComparison();
        }

        private void GenerateComparison()
        {
            ComparisonResult.Clear();
            string input = UserInputText ?? "";
            string target = CorrectAnswer ?? "";

            // Lấy độ dài lớn nhất để duyệt hết cả 2 chuỗi
            int length = System.Math.Max(input.Length, target.Length);

            for (int i = 0; i < length; i++)
            {
                string charToDisplay = "";
                string color = "#FFFFFF"; // Mặc định trắng
                string weight = "Normal";

                if (i < input.Length)
                {
                    charToDisplay = input[i].ToString();

                    if (i < target.Length)
                    {
                        // Có ký tự ở cả input và target -> So sánh
                        if (char.ToLower(input[i]) == char.ToLower(target[i]))
                        {
                            color = "#2ECC71"; // Đúng (Xanh lá)
                        }
                        else
                        {
                            color = "#FF5252"; // Sai (Đỏ)
                            weight = "Bold";
                        }
                    }
                    else
                    {
                        // Input dài hơn Target -> Thừa (Đỏ)
                        color = "#FF5252";
                        weight = "Bold";
                    }
                }
                else
                {
                    // Input ngắn hơn Target -> Thiếu
                    // (Tùy chọn: Có thể hiển thị dấu gạch dưới _ màu đỏ để báo thiếu)
                    continue;
                }

                ComparisonResult.Add(new ComparisonChar
                {
                    Character = charToDisplay,
                    Color = color,
                    FontWeight = weight
                });
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

        [RelayCommand]
        private async Task AgainAsync()
        {
            await ProcessReview(ReviewOutcome.Again);
        }
        [RelayCommand]
        private async Task HardAsync()
        {
            await ProcessReview(ReviewOutcome.Hard);
        }
        [RelayCommand]
        private async Task GoodAsync()
        {
            await ProcessReview(ReviewOutcome.Good);
        }
        [RelayCommand]
        private async Task EasyAsync()
        {
            await ProcessReview(ReviewOutcome.Easy);
        }

        // Hàm helper xử lý outcome: gọi service cập nhật, sau đó tải thẻ tiếp theo
        private async Task ProcessReview(ReviewOutcome outcome)
        {
            if (_currentCard == null) return;
            await _studyService.ProcessReviewAsync(_currentCard, outcome);
            await LoadNextCardAsync();
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