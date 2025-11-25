using EasyFlips.ViewModels;
using System.Windows;

namespace EasyFlips
{
    /// <summary>
    /// Interaction logic for CreateDeckWindow.xaml
    /// </summary>
    public partial class CreateDeckWindow : Window
    {
        // Yêu cầu "bộ não" (ViewModel) qua constructor
        public CreateDeckWindow(CreateDeckViewModel viewModel)
        {
            InitializeComponent();

            // Gán "bộ não" (DI đã tự động tạo nó cho bạn)
            this.DataContext = viewModel;
        }
        // Thêm constructor không tham số (overload)
        public CreateDeckWindow()
            : this(new CreateDeckViewModel())
        {
        }
    }
}
