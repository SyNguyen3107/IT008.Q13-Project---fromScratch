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
using IT008.Q13_Project___fromScratch.ViewModels;

namespace IT008.Q13_Project___fromScratch.Views
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
