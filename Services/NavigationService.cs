using IT008.Q13_Project___fromScratch.Interfaces;
using IT008.Q13_Project___fromScratch.Services;
using IT008.Q13_Project___fromScratch.ViewModels;
using IT008.Q13_Project___fromScratch.Views; 
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;

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
            var window = _serviceProvider.GetRequiredService<AddCardWindow>();
            window.ShowDialog();
        }

        public void ShowCardWindow()
        {
            //Thêm lệnh mở cửa sổ ShowCardWindow
        }
        public void ShowCreateDeckWindow()
        {
            // Yêu cầu "thợ điện" tạo một cửa sổ mới
            // DI sẽ tự động tìm CreateDeckWindow và CreateDeckViewModel
            var window = _serviceProvider.GetRequiredService<CreateDeckWindow>();

            // ShowDialog() để nó chặn cửa sổ chính, bắt người dùng phải tương tác
            window.ShowDialog();
        }

        // Thực thi việc mở cửa sổ Học
        public void ShowStudyWindow(int deckId) // Bỏ 'async void'
        {
            // 1. Yêu cầu DI tạo ra StudyWindow.
            //    DI sẽ TỰ ĐỘNG tạo StudyViewModel VÀ tiêm nó vào constructor
            //    của StudyWindow. (Chỉ tạo 1 ViewModel)
            var window = _serviceProvider.GetRequiredService<StudyWindow>();

            // 2. Lấy ViewModel đã được tiêm vào từ DataContext của cửa sổ
            //    (Điều này yêu cầu StudyWindow.xaml.cs phải có: DataContext = viewModel;)
            if (window.DataContext is not StudyViewModel viewModel)
            {
                // Lỗi này không bao giờ nên xảy ra nếu DI setup đúng
                Debug.WriteLine("LỖI NGHIÊM TRỌNG: StudyWindow không có StudyViewModel trong DataContext!");
                return;
            }

            // 3. Đăng ký sự kiện "Loaded" của cửa sổ.
            //    Đây là nơi "async void" AN TOÀN và ĐÚNG CHỖ.
            window.Loaded += async (sender, e) =>
            {
                try
                {
                    // 4. Ra lệnh cho ViewModel (đã tồn tại) tải dữ liệu
                    await viewModel.InitializeAsync(deckId);
                }
                catch (Exception ex)
                {
                    // 5. Bắt lỗi nếu tải CSDL thất bại (app không bị crash)
                    Debug.WriteLine($"Lỗi khi tải thẻ học: {ex.Message}");
                    MessageBox.Show("Không thể tải bộ thẻ. Vui lòng thử lại.", "Lỗi");
                    window.Close(); // Đóng cửa sổ học
                }
            };

            // 6. Hiển thị cửa sổ
            window.Show();
        }
        public void ImportFileWindow()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Import";
            openFileDialog.Filter = "All supported formats (*.apkg, *.txt, *.zip)|*.apkg;*.txt;*.zip" +
                                    "|Anki Deck Package (*.apkg)|*.apkg" +
                                    "|Text file (*.txt)|*.txt" +
                                    "|Zip file (*.zip)|*.zip";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                Debug.WriteLine($"File to import: {selectedFilePath}");
                // TODO: Gọi service để xử lý file (ví dụ: _importService.Import(selectedFilePath))
            }
            else
            {
                // Người dùng đã nhấn "Cancel"
            }
        }
        public void ShowSyncWindow()
        {
            throw new NotImplementedException();
        }

        public void ShowDeckChosenWindow(int deckId)
        {
            var window = _serviceProvider.GetService<DeckChosenWindow>();
            var viewModel = window.DataContext as DeckChosenViewModel;

            if (viewModel != null)
            {
                viewModel.InitializeAsync(deckId);
            }
            // Thiết lập cửa sổ cha và vị trí khởi động
            window.Owner = Application.Current.MainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //Chặn cửa sổ chính khi cửa sổ con đang mở
            window.ShowDialog();
        }
    }
}