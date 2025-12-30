using EasyFlips.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class HostGameWindow : Window
    {
        private bool _canClose = false;

        public HostGameWindow()
        {
            InitializeComponent();

            // Lắng nghe DataContext để gán Action đóng cửa sổ
            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is HostGameViewModel vm)
                {
                    vm.CloseWindowAction = () =>
                    {
                        _canClose = true; // Mở chốt an toàn
                        this.Close();     // Đóng cửa sổ
                    };
                }
            };
        }
    }
}