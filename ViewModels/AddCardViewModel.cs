using IT008.Q13_Project___fromScratch.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.Repositories;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    internal class AddCardViewModel //: ICardRepository
    {
            var decks = await _deckRepository.GetAllAsync();
            Decks.Clear();
            foreach (var deck in decks)
                Decks.Add(deck);
        }
    }
}
