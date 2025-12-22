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
using EasyFlips.ViewModels;

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for MemberGameWindow.xaml
    /// </summary>
    public partial class MemberGameWindow : Window
    {
        public MemberGameWindow()
        {
            InitializeComponent();
            // Gắn "Bộ não" (ViewModel) vào "Giao diện" (Window)
            this.DataContext = new MemberGameViewModel();
        }
    }
}
