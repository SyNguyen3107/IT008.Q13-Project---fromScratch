using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IT008.Q13_Project___fromScratch.Repositories;
using IT008.Q13_Project___fromScratch.ViewModels;

namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for Add.xaml
    /// </summary>
    public partial class AddCardWindow : Window
    {
        public AddCardWindow()
        {
            InitializeComponent();
            // Tạo repository thật
            ICardRepository cardRepository = new CardRepository();
            IDeckRepository deckRepository = new DeckRepository();

            // Khởi tạo ViewModel và gán DataContext
            DataContext = new AddCardViewModel(cardRepository, deckRepository);
        }
    }
}
