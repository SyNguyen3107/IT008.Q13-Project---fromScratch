using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using EasyFlips.Helpers;

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
            Debug.WriteLine($"Playing audio from: {filePath}");
            try
            {
                Uri uri;
                if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    uri = new Uri(filePath);
                }
                else
                {
                    if (!Path.IsPathRooted(filePath))
                    { string appMediaFolder = PathHelper.GetMediaFolderPath(); 
                        filePath = Path.Combine(appMediaFolder, filePath); 
                    }

                        if (!File.Exists(filePath)) return;
                    uri = new Uri(filePath, UriKind.Absolute);
                }

                _mediaPlayer.Open(uri);
                _mediaPlayer.Play();
            }
            catch (Exception)
            {
                
            }
        }
        public void PlayLoopingAudio(string relativePath)
        {
            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show("MP3 file not found.");
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