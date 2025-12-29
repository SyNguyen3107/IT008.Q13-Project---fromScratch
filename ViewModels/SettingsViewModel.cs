using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;

        public ObservableCollection<Deck> AvailableDecks { get; } = new ObservableCollection<Deck>();

        [ObservableProperty] private Deck _selectedDeck;

        [ObservableProperty] private double _maxPlayersDouble;

        [ObservableProperty] private int _timePerRound;

        [ObservableProperty] private int _waitTimeMinutes;

        public int MaxPlayers
        {
            get => (int)Math.Round(MaxPlayersDouble);
            set => MaxPlayersDouble = value;
        }

  
        partial void OnMaxPlayersDoubleChanged(double value)
        {
            OnPropertyChanged(nameof(MaxPlayers));
        }

        public SettingsViewModel(IDeckRepository deckRepository, Deck currentDeck, int currentMaxPlayers, int currentTimePerRound, int currentWaitTimeSeconds)
        {
            _deckRepository = deckRepository;

        
            _maxPlayersDouble = currentMaxPlayers;
            _timePerRound = currentTimePerRound;
            _waitTimeMinutes = currentWaitTimeSeconds / 60;


            _ = LoadDecksAsync(currentDeck);
        }

        private async Task LoadDecksAsync(Deck currentDeck)
        {
            var decks = await _deckRepository.GetAllAsync();
            AvailableDecks.Clear();
            foreach (var d in decks) AvailableDecks.Add(d);

            SelectedDeck = currentDeck ?? AvailableDecks.FirstOrDefault();
        }

        [RelayCommand]
        public void Save(Window window)
        {
            if (window != null)
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        [RelayCommand]
        public void Cancel(Window window)
        {
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
