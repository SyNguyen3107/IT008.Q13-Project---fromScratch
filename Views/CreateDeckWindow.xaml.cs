﻿using System.Windows;
using IT008.Q13_Project___fromScratch.Repositories;
using IT008.Q13_Project___fromScratch.ViewModels;

namespace IT008.Q13_Project___fromScratch
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
    }
}
