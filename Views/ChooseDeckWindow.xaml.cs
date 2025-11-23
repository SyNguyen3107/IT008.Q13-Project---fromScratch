using System.Windows;
using IT008.Q13_Project___fromScratch.ViewModels; // Import ViewModel
using IT008.Q13_Project___fromScratch.Models;     // Import Model

namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for ChooseDeckWindow.xaml
    /// </summary>
    public partial class ChooseDeckWindow : Window
    {
        // Property để bên ngoài lấy kết quả sau khi chọn
        public Deck SelectedDeck { get; private set; }

        // Constructor nhận ViewModel từ DI
        public ChooseDeckWindow(ChooseDeckViewModel viewModel)
        {
            InitializeComponent();

            // Gán DataContext
            this.DataContext = viewModel;

            // Đăng ký sự kiện đóng cửa sổ để lấy kết quả từ ViewModel
            // (ViewModel sẽ gán kết quả vào Window.Tag trước khi đóng)
            this.Closing += (s, e) =>
            {
                if (this.DialogResult == true && this.Tag is Deck deck)
                {
                    SelectedDeck = deck;
                }
            };
        }
    }
}