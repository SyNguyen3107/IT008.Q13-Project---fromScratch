using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class SyncWindow : Window
    {
        public SyncWindow(SyncViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}