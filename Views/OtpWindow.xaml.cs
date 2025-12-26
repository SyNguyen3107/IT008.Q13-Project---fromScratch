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

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for OtpWindow.xaml
    /// </summary>
    public partial class OtpWindow : Window
    {
        public OtpWindow()
        {
            InitializeComponent();

        }
        private void OtpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox.Text.Length == textBox.MaxLength)
            {
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

    }
}
