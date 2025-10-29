using CommunityToolkit.Mvvm.ComponentModel;
using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public partial class AddCardViewModel //: ICardRepository
    {
        private readonly ICardRepository _cardRepository;

        // DI sẽ tiêm CardRepository vào đây
        public AddCardViewModel(ICardRepository cardRepository)
        {
            _cardRepository = cardRepository;
        }

        // ... (Các thuộc tính cho FrontText, BackText... và Command "Save" ở đây) ...
    }
}
