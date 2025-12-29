using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyFlips.Interfaces;
using EasyFlips.Models;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class DeckRenameViewModel : ObservableObject
    {
        private readonly IDeckRepository _deckRepository;
        private readonly IMessenger _messenger;
        private Deck _targetDeck;

       
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
        private string _newName = string.Empty;

  
        public DeckRenameViewModel(IDeckRepository deckRepository, IMessenger messenger)
        {
            _deckRepository = deckRepository;
            _messenger = messenger;
        }

    
        public void Initialize(Deck deck)
        {
            _targetDeck = deck;
            NewName = deck.Name; 
        }

        
        private bool CanRename()
        {
            return !string.IsNullOrWhiteSpace(NewName);
        }

        
        [RelayCommand(CanExecute = nameof(CanRename))]
        private async Task Rename(object? param)
        {
            if (_targetDeck == null) return;

            
            _targetDeck.Name = NewName;

            
            await _deckRepository.UpdateAsync(_targetDeck);

            
            _messenger.Send(new DeckUpdatedMessage(_targetDeck));

            
            if (param is Window window)
            {
                window.Close();
            }
        }

        
        [RelayCommand]
        private void Cancel(object? param)
        {
            if (param is Window window)
            {
                window.Close();
            }
        }
    }
}