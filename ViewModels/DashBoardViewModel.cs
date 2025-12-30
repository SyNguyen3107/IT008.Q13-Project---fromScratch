using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using EasyFlips.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace EasyFlips.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IDeckRepository _deckRepository;

        public DashboardStats Stats { get; set; } = new DashboardStats();

        private double _newWidth;
        public double NewWidth { get => _newWidth; set => SetProperty(ref _newWidth, value); }

        private double _learningWidth;
        public double LearningWidth { get => _learningWidth; set => SetProperty(ref _learningWidth, value); }

        private double _masteredWidth;
        public double MasteredWidth { get => _masteredWidth; set => SetProperty(ref _masteredWidth, value); }
        private string _activeTime = "0h";
        public string ActiveTime { get => _activeTime; set => SetProperty(ref _activeTime, value); }
        private string _nextReviewText = "No reviews scheduled";
        public string NextReviewText
        {
            get => _nextReviewText;
            set => SetProperty(ref _nextReviewText, value);
        }

        public ObservableCollection<double> WeeklyHeights { get; set; } =
            new ObservableCollection<double> { 0, 0, 0, 0, 0, 0, 0 };

        private readonly INavigationService _navigationService;


        public DashboardViewModel(IDeckRepository deckRepository, INavigationService navigationService)
        {
            _deckRepository = deckRepository;
            _navigationService = navigationService; 
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var decks = (await _deckRepository.GetAllAsync())?.ToList();
            if (decks == null || !decks.Any()) return;

            var allCards = decks.SelectMany(d => d.Cards ?? new List<Card>()).ToList();
            var allProgress = allCards.Where(c => c.Progress != null).Select(c => c.Progress).ToList();
            double totalCards = allCards.Count > 0 ? allCards.Count : 1;

            Stats.New = decks.Sum(d => d.NewCount);
            Stats.Learning = decks.Sum(d => d.LearnCount);
            Stats.DueToday = decks.Sum(d => d.DueCount);
            Stats.Review = allProgress.Count(p => p.Interval >= 21);

            Stats.MemoryStrength = (int)((double)Stats.Review / (double)totalCards * 100);

            var reviewDates = allProgress.Select(p => p.LastReviewDate).ToList();
            Stats.Streak = CalculateStreak(reviewDates);


            var today = DateTime.Today;
            var countsPerDay = new List<int>(); 

            for (int i = 0; i < 7; i++)
            {
                var targetDate = today.AddDays(-(6 - i));


                int count = allProgress.Count(p => p.LastReviewDate.Date == targetDate.Date);
                countsPerDay.Add(count);

                WeeklyHeights[i] = Math.Min(140, (count / 50.0) * 140);
            }


            double totalMinutes = countsPerDay.Sum() * 1.5; 
            ActiveTime = $"Active: {Math.Round(totalMinutes / 60, 1)}h";

            NewWidth = (Stats.New / totalCards) * 250;
            LearningWidth = (Stats.Learning / totalCards) * 250;
            MasteredWidth = (Stats.Review / totalCards) * 250;

            OnPropertyChanged(nameof(Stats));
            OnPropertyChanged(nameof(WeeklyHeights));
            OnPropertyChanged(nameof(ActiveTime));

            var upcomingReviews = allCards
                .Where(c => c.Progress != null)
                .Select(c => c.Progress.LastReviewDate.AddDays(c.Progress.Interval))
                .Where(due => due > DateTime.Now)
                .OrderBy(due => due)
                .ToList();

            if (upcomingReviews.Any())
            {
                var nextDue = upcomingReviews.First();
                var timeDiff = nextDue - DateTime.Now;

                if (timeDiff.TotalDays >= 1)
                    NextReviewText = $"Next review in {Math.Round(timeDiff.TotalDays, 0)}d";
                else if (timeDiff.TotalHours >= 1)
                    NextReviewText = $"Next review in {Math.Round(timeDiff.TotalHours, 0)}h";
                else
                    NextReviewText = $"Next review in {Math.Round(timeDiff.TotalMinutes, 0)}m";
            }

            OnPropertyChanged(nameof(NextReviewText));
        }

        private int CalculateStreak(IEnumerable<DateTime> reviewDates)
        {
            if (reviewDates == null || !reviewDates.Any()) return 0;

            var sortedDates = reviewDates
                .Select(d => d.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var today = DateTime.Today;
            if (sortedDates[0] < today.AddDays(-1)) return 0;

            int streak = 0;
            DateTime expectedDate = sortedDates[0];

            foreach (var date in sortedDates)
            {
                if (date == expectedDate)
                {
                    streak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else break;
            }
            return streak;
        }
        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateToHome();
        }
    }
}