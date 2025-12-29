using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces; 
using EasyFlips.Models;
using System.Collections.ObjectModel;
using System.Windows;
namespace EasyFlips.ViewModels
{
    public partial class ChooseDeckViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private ObservableCollection<Deck> decks = new();

       
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChooseCommand))] 
        private Deck? _selectedDeck;

      
        public ChooseDeckViewModel(IDeckRepository deckRepository, INavigationService navigationService)
        {
            _deckRepository = deckRepository;

            
            _ = LoadDecksAsync();
            _navigationService = navigationService;
        }

      
        [RelayCommand]
        private void Choose(Window window)
        {
            if (SelectedDeck == null)
            {
                MessageBox.Show("Please choose a deck!", "No deck selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

        
            window.Tag = SelectedDeck;
            window.DialogResult = true;
            window.Close();
        }

        
        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }

        [RelayCommand]
        private void Add()
        {
            _navigationService.ShowCreateDeckWindow();
            _ = LoadDecksAsync(); 
        }
     
        public void LoadDecks(IEnumerable<Deck> deckList)
        {
            Decks.Clear();
            foreach (var deck in deckList)
                Decks.Add(deck);
        }
        public async Task LoadDecksAsync()
        {
            
            var deckList = await _deckRepository.GetAllAsync();

            Decks.Clear();
            foreach (var deck in deckList)
            {
                Decks.Add(deck);
            }
        }
    }
}
