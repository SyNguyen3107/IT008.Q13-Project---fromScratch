using System;
using System.IO;
using System.Windows.Media; // Cần tham chiếu đến PresentationCore và WindowsBase

namespace IT008.Q13_Project___fromScratch.Services
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

        public void StopAudio()
        {
            _mediaPlayer.Stop();
        }
    }
}