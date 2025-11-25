using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for DeckRenameWindow.xaml
    /// </summary>
    public partial class DeckRenameWindow : Window
    {
        // Constructor nhận ViewModel từ bên ngoài (DI hoặc tạo mới từ MainViewModel)
        public DeckRenameWindow(DeckRenameViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

    }
}
