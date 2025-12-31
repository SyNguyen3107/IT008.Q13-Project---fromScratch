using EasyFlips.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
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
    /// Interaction logic for HostLeaderboardWindow.xaml
    /// </summary>
    public partial class HostLeaderboardWindow : Window
    {
        public HostLeaderboardWindow(HostLeaderboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // [QUAN TRỌNG] Gán hành động đóng cửa sổ
            if (viewModel.CloseAction == null)
            {
                viewModel.CloseAction = new Action(this.Close);
            }
        }
        private void OnSaveImageClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Lấy kích thước thực tế của cửa sổ
                int width = (int)this.ActualWidth;
                int height = (int)this.ActualHeight;

                // 2. Render visual thành bitmap
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(this);

                // 3. Tạo Encoder (PNG)
                PngBitmapEncoder pngImage = new PngBitmapEncoder();
                pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                // --- [SỬA ĐOẠN NÀY] ---

                // B1: Lấy đường dẫn AppData/Roaming
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                // B2: Kết hợp tạo đường dẫn thư mục mong muốn: .../AppData/Roaming/EasyFlips/Record
                string folderPath = System.IO.Path.Combine(appDataPath, "EasyFlips", "Record");

                // B3: Kiểm tra và TẠO THƯ MỤC nếu chưa tồn tại (Rất quan trọng)
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }

                // B4: Tạo đường dẫn file đầy đủ
                string fileName = $"Leaderboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string filePath = System.IO.Path.Combine(folderPath, fileName);

                // -----------------------

                // 4. Lưu ra file
                using (Stream fileStream = File.Create(filePath))
                {
                    pngImage.Save(fileStream);
                }

                // 5. Thông báo thành công
                // (Tùy chọn: Hỏi user có muốn mở thư mục vừa lưu không)
                var result = MessageBox.Show(
                    $"Đã lưu ảnh thành công!\nĐường dẫn: {filePath}\n\nBạn có muốn mở thư mục chứa ảnh không?",
                    "Lưu thành công",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Mở thư mục lên cho user xem
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
