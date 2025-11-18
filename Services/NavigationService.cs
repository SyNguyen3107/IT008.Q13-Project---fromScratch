using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Views; 
using Microsoft.Extensions.DependencyInjection; 
using IT008.Q13_Project___fromScratch.ViewModels;
using IT008.Q13_Project___fromScratch.Services;
using System;

namespace IT008.Q13_Project___fromScratch.Services
{
    public class NavigationService : INavigationService
    {
        //DI chính sẽ được tiêm vào đây
        private readonly IServiceProvider _serviceProvider;

        // Constructor: Nhận DI
        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void ShowAddCardWindow()
        {
            //Thêm lệnh mở cửa sổ ShowAddCardWindow
        }

        public void ShowCardWindow()
        {
            //Thêm lệnh mở cửa sổ ShowCardWindow
        }

        // Thực thi việc mở cửa sổ Create Deck
        public void ShowCreateDeckWindow()
        {
            // Yêu cầu "thợ điện" tạo một cửa sổ mới
            // DI sẽ tự động tìm CreateDeckWindow và CreateDeckViewModel
            var window = _serviceProvider.GetRequiredService<CreateDeckWindow>();

            // ShowDialog() để nó chặn cửa sổ chính, bắt người dùng phải tương tác
            window.ShowDialog();
        }

        // Thực thi việc mở cửa sổ Học
        public async void ShowStudyWindow(int deckId)
        {
            // 1. Yêu cầu "thợ điện" tạo ra "bộ não" (ViewModel)
            var viewModel = _serviceProvider.GetRequiredService<StudyViewModel>();

            // 2. Tải dữ liệu cho "bộ não" đó
            await viewModel.InitializeAsync(deckId);
            // 3. Yêu cầu "thợ điện" tạo "khuôn mặt" (View)
            var window = _serviceProvider.GetRequiredService<StudyWindow>();

            // 4. Lắp "bộ não" vào "khuôn mặt"
            window.DataContext = viewModel;

            window.Show();
        }

        public void ShowSyncWindow()
        {
            throw new NotImplementedException();
        }
    }
}