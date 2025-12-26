using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class MemberGameWindow : Window
    {
        public MemberGameWindow(MemberGameViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}