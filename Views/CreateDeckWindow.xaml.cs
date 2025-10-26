using System.Windows;
using IT008.Q13_Project___fromScratch.Repositories;
using IT008.Q13_Project___fromScratch.ViewModels;

namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for CreateDeckWindow.xaml
    /// </summary>
    public partial class CreateDeckWindow : Window
    {
        public CreateDeckWindow()
        {
            InitializeComponent();

            // (1) Khởi tạo Repository
            var deckRepository = new DeckRepository();

            // (2) Gán ViewModel cho DataContext
            this.DataContext = new CreateDeckViewModel(deckRepository);
        }
    }
}
