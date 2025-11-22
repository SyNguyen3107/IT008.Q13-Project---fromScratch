using IT008.Q13_Project___fromScratch.ViewModels;
using System.Windows;

namespace IT008.Q13_Project___fromScratch.Views
{
    /// <summary>
    /// Interaction logic for DeckChosenWindow.xaml
    /// </summary>
    public partial class DeckChosenWindow : Window
    {
        // ViewModel sẽ được tiêm bởi DI khi tạo Window
        public DeckChosenWindow(DeckChosenViewModel viewModel)
        {
            InitializeComponent();

            // Gán DataContext để XAML có thể bind tới ViewModel
            DataContext = viewModel;
        }
    }
}
