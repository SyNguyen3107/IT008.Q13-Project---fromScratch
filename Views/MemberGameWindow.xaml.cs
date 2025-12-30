using EasyFlips.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace EasyFlips.Views
{
    public partial class MemberGameWindow : Window
    {
        public MemberGameWindow()
        {
            InitializeComponent();

            // Lắng nghe DataContext để gắn kết Action đóng cửa sổ khi NavigationService gán ViewModel
            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is MemberGameViewModel vm)
                {
                    vm.CloseWindowAction = () =>
                    {
                        this.Close();     // Thực hiện đóng cửa sổ
                    };
                }
            };
        }
    }
}