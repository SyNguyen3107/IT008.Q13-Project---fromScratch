using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Services;
using System.Windows;

namespace EasyFlips.ViewModels
{
   
    public partial class DeckChosenViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private readonly INavigationService _navigationService;

        private string _deckId;

        private string _deckName = "Loading..";

        private int _newCount;


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

 
        public int LearningCount
        {
            get => _learningCount;
            set => SetProperty(ref _learningCount, value);
        }

        private int _reviewCount;

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


        public async Task InitializeAsync(string deckId)
        {
            _deckId = deckId; 


          
            var stats = await _studyService.GetDeckStatsAsync(deckId);

          
            DeckName = stats.DeckName ?? "Unknown";
            NewCount = stats?.NewCount ?? 0;
            LearningCount = stats?.LearningCount ?? 0;
            ReviewCount = stats?.ReviewCount ?? 0;
        }

       
        [RelayCommand]
        private void StudyNow(Window window)
        {
           
            int totalCardsToStudy = NewCount + LearningCount + ReviewCount;

            if (totalCardsToStudy > 0)
            {
               
                _navigationService.ShowStudyWindow(_deckId);

                if (window != null)
                {
                    window.Close();
                }
            }
            else
            {
                
                MessageBox.Show("You have already completed this deck!\nPlease comeback later.",
                                "Completed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
        }
    }
}
