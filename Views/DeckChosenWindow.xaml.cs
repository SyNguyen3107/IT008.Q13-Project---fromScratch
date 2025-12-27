using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
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
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
