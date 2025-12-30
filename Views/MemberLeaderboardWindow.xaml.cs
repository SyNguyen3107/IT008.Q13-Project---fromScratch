using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for MemberLeaderboardWindow.xaml
    /// </summary>
    public partial class MemberLeaderboardWindow : Window
    {
        public MemberLeaderboardWindow()
        {
            InitializeComponent();
        }
        private void OnSaveImageClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Lấy kích thước thực tế của cửa sổ
                int width = (int)this.ActualWidth;
                int height = (int)this.ActualHeight;

                // Render visual thành bitmap
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(this);

                // Tạo Encoder (PNG)
                PngBitmapEncoder pngImage = new PngBitmapEncoder();
                pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                // Lưu ra file
                string fileName = $"Leaderboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), fileName);

                using (Stream fileStream = File.Create(filePath))
                {
                    pngImage.Save(fileStream);
                }

                MessageBox.Show($"Đã lưu ảnh tại: {filePath}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
