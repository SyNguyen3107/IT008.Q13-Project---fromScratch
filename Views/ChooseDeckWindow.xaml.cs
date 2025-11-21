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
using IT008.Q13_Project___fromScratch.Models;
using IT008.Q13_Project___fromScratch.ViewModels;

namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for ChooseDeckWindow.xaml
    /// </summary>
    public partial class ChooseDeckWindow : Window
    {
        public Deck SelectedDeck { get; set; }
        public ChooseDeckWindow() : this(new List<Deck>()) { }
        public ChooseDeckWindow(IEnumerable<Deck> deckList)
        {
            InitializeComponent();
            var vm = new ChooseDeckViewModel();
            vm.LoadDecks(deckList);
            DataContext = vm;
        }
    }
}
