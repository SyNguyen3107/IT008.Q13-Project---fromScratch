using System.Windows;

namespace EasyFlips.Views
{
    public partial class ErrorDialogWindow : Window
    {
        public ErrorDialogWindow(string errorMessage)
        {
            InitializeComponent();
            ErrorMessageText.Text = errorMessage;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
