using System.IO;
using System.Windows;
using System.Windows.Media; // Cần tham chiếu đến PresentationCore và WindowsBase

namespace EasyFlips.Services
{
    public class AudioService
    {
        private readonly MediaPlayer _mediaPlayer;

        public AudioService()
        {
            _mediaPlayer = new MediaPlayer();
        }

        public void PlayAudio(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                // Xử lý đường dẫn
                Uri uri;
                if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    uri = new Uri(filePath);
                }
                else
                {
                    if (!File.Exists(filePath)) return;
                    uri = new Uri(filePath, UriKind.Absolute);
                }

                _mediaPlayer.Open(uri);
                _mediaPlayer.Play();
            }
            catch (Exception)
            {
                // Có thể log lỗi hoặc bỏ qua nếu file lỗi
            }
        }
        public void PlayLoopingAudio(string relativePath)
        {
            // relativePath ví dụ: "Resources/Sound/Lobby.mp3"
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show("Không tìm thấy file mp3");
                return;
            }

            _mediaPlayer.Open(new Uri(fullPath, UriKind.Absolute));
            _mediaPlayer.MediaEnded += (s, e) =>
            {
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            };
            _mediaPlayer.MediaFailed += (s, e) =>
            {
                MessageBox.Show($"Media error: {e.ErrorException.Message}");
            };

            _mediaPlayer.Play();
        }

        public void PlayOneShot(string relativePath)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return;

            var player = new MediaPlayer();
            player.Open(new Uri(fullPath, UriKind.Absolute));
            player.Play();

            // Giải phóng sau khi phát xong
            player.MediaEnded += (s, e) =>
            {
                player.Close();
            };
        }






        public void StopAudio()
        {
            _mediaPlayer.Stop();
        }
    }
}