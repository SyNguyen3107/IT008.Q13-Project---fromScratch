using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace EasyFlips.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;

        public DashboardStats Stats { get; set; } = new DashboardStats();

        private double _newWidth;
        public double NewWidth { get => _newWidth; set => SetProperty(ref _newWidth, value); }

        private double _learningWidth;
        public double LearningWidth { get => _learningWidth; set => SetProperty(ref _learningWidth, value); }

        private double _masteredWidth;
        public double MasteredWidth { get => _masteredWidth; set => SetProperty(ref _masteredWidth, value); }

        private string _activeTime = "0m";
        public string ActiveTime { get => _activeTime; set => SetProperty(ref _activeTime, value); }

        private string _nextReviewText = "No reviews scheduled";
        public string NextReviewText
        {
            get => _nextReviewText;
            set => SetProperty(ref _nextReviewText, value);
        }

        public ObservableCollection<double> WeeklyHeights { get; set; } =
            new ObservableCollection<double> { 0, 0, 0, 0, 0, 0, 0 };

        public DashboardViewModel(IDeckRepository deckRepository, INavigationService navigationService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService;
            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                var decks = (await _deckRepository.GetAllAsync())?.ToList();
                if (decks == null || !decks.Any()) return;

                var allCards = decks.SelectMany(d => d.Cards ?? new List<Card>()).ToList();
                var now = DateTime.Now;
                double totalCards = allCards.Count > 0 ? (double)allCards.Count : 1.0;

                // --- 1. PHÂN LOẠI TRẠNG THÁI ---
                Stats.New = allCards.Count(c => c.Progress == null);
                Stats.Learning = allCards.Count(c => c.Progress != null && c.Progress.Interval < 1);
                Stats.DueToday = allCards.Count(c => c.Progress != null && c.Progress.DueDate <= now);

                // Mastered: Tính từ Interval >= 3 ngày để thanh bar có màu sớm
                int masteredCount = allCards.Count(c => c.Progress != null && c.Progress.Interval >= 3);
                Stats.Review = masteredCount;

                // --- 2. TÍNH STRENGTH & MASTERY XP (DÙNG VÒNG LẶP ĐỂ TRÁNH LỖI) ---
                double totalMemoryScore = 0;
                double totalXp = 0;
                var learnedCards = allCards.Where(c => c.Progress != null).ToList();

                foreach (var card in learnedCards)
                {
                    // Tính XP: Repetitions * EaseFactor
                    
                    double factor = (card.Progress.EaseFactor == 0) ? 2.5 : card.Progress.EaseFactor;

                    // Sau đó thực hiện tính toán
                    totalXp += (card.Progress.Repetitions * factor);

                    // Tính Memory Score cho từng thẻ (Interval 21 ngày = 100%)
                    totalMemoryScore += Math.Min(100, (card.Progress.Interval / 21.0) * 100);
                }

                // Gán kết quả (Sử dụng ép kiểu int rõ ràng)
                Stats.Streak = (int)totalXp; // Đây là Mastery XP
                Stats.MemoryStrength = learnedCards.Any() ? (int)(totalMemoryScore / totalCards) : 0;

                // --- 3. BIỂU ĐỒ & THỜI GIAN ---
                var reviewDates = learnedCards
                    .Where(c => c.Progress.LastReviewDate != default(DateTime))
                    .Select(c => c.Progress.LastReviewDate)
                    .ToList();

                var today = DateTime.Today;
                for (int i = 0; i < 7; i++)
                {
                    var targetDate = today.AddDays(-(6 - i));
                    int count = reviewDates.Count(d => d.Date == targetDate.Date);
                    WeeklyHeights[i] = Math.Min(140, (count / 15.0) * 140);
                }

                double totalMins = reviewDates.Count * 0.25;
                ActiveTime = totalMins < 60 ? $"{Math.Round(totalMins, 0)}m" : $"{Math.Round(totalMins / 60, 1)}h";

                // --- 4. ĐỘ RỘNG THANH BAR ---
                NewWidth = (Stats.New / totalCards) * 250;
                LearningWidth = (Stats.Learning / totalCards) * 250;
                MasteredWidth = (masteredCount / totalCards) * 250;

                UpdateNextReviewText(allCards);

                // --- 5. THÔNG BÁO UI ---
                OnPropertyChanged(nameof(Stats));
                OnPropertyChanged(nameof(ActiveTime));
                OnPropertyChanged(nameof(NewWidth));
                OnPropertyChanged(nameof(LearningWidth));
                OnPropertyChanged(nameof(MasteredWidth));
                OnPropertyChanged(nameof(WeeklyHeights));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard] Error: {ex.Message}");
            }
        }

        private void UpdateNextReviewText(List<Card> allCards)
        {
            var nextDue = allCards
                .Where(c => c.Progress != null)
                .Select(c => c.Progress.DueDate)
                .Where(due => due > DateTime.Now)
                .OrderBy(due => due)
                .FirstOrDefault();

            if (nextDue != default(DateTime))
            {
                var diff = nextDue - DateTime.Now;
                if (diff.TotalDays >= 1) NextReviewText = $"Next in {Math.Round(diff.TotalDays, 0)}d";
                else if (diff.TotalHours >= 1) NextReviewText = $"Next in {Math.Round(diff.TotalHours, 0)}h";
                else NextReviewText = $"Next in {Math.Round(diff.TotalMinutes, 0)}m";
            }
            else
            {
                NextReviewText = "No reviews scheduled";
            }
            OnPropertyChanged(nameof(NextReviewText));
        }

        [RelayCommand]
        private void GoBack() => _navigationService.NavigateToHome();
    }
}