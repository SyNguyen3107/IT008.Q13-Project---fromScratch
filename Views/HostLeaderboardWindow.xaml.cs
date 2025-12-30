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

namespace EasyFlips.Views
{
    /// <summary>
    /// Interaction logic for HostLeaderboardWindow.xaml
    /// </summary>
    public partial class HostLeaderboardWindow : Window
    {
        public HostLeaderboardWindow()
        {
            InitializeComponent();
        }
        private void OnSaveImageClick(object sender, RoutedEventArgs e)
        {
            // Render Visual thành Bitmap
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                (int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(this);

            // Lưu File
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = $"Leaderboard_{DateTime.Now:yyyyMMdd_HHmmss}";
            dlg.DefaultExt = ".png";
            dlg.Filter = "PNG Image (.png)|*.png";

            if (dlg.ShowDialog() == true)
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
                using (var stream = System.IO.File.Create(dlg.FileName))
                {
                    encoder.Save(stream);
                }
                MessageBox.Show("Đã lưu ảnh thành công!");
            }
        }
    }
}
