using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace IT008.Q13_Project___fromScratch
{
    /// <summary>
    /// Interaction logic for MainAnkiWindow.xaml
    /// </summary>
    public partial class MainAnkiWindow : Window
    {
        public MainAnkiWindow()
        {
            InitializeComponent();
        }

        private void ImportFileWindowOpen(object sender, RoutedEventArgs e)
        {
            //Cửa sổ Import File
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Import";
            openFileDialog.Filter = "All supported formats (*.apkg, *.txt, *.zip)|*.apkg;*.txt;*.zip" +
                                "|Anki Deck Package (*.apkg)|*.apkg" +
                                "|Text file (*.txt)|*.txt" +
                                "|Zip file (*.zip)|*.zip";
            //Thư mục mở mặc định, tạm thời để là My Document
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                //Lấy đường dẫn file người dùng đã chọn
                string selectedFilePath = openFileDialog.FileName;

                // TODO: Gọi service hoặc phương thức để xử lý file này
                Debug.WriteLine($"File to import: {selectedFilePath}");
            }
            else
            {
                // Người dùng đã nhấn "Cancel"
            }
        }

        private void CreateDeckWindowWindowOpen(object sender, RoutedEventArgs e)
        {
            CreateDeckWindow a = new CreateDeckWindow();
        }
    }
}

