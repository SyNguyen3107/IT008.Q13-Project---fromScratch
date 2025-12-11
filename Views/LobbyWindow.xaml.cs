using System.Windows;
using EasyFlips.ViewModels;

namespace EasyFlips.Views
{
    public partial class LobbyWindow : Window
    {
        public LobbyWindow(LobbyViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}