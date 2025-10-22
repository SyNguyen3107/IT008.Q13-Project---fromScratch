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

namespace fromScratch_project
{
    /// <summary>
    /// Interaction logic for StudyWindow.xaml
    /// </summary>
    public partial class StudyWindow : Window
    {
        public StudyWindow(StudyViewModel viewModel)
        {
            InitializeComponent();

            // 2. Gán DataContext:
            // Đây là dòng "ma thuật" kết nối View với ViewModel.
            this.DataContext = viewModel;
        }
    }
}
