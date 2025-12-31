using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using EasyFlips.Helpers;

namespace EasyFlips.Services
{
    public class AudioService
    {
        // Chỉ dùng 1 MediaPlayer duy nhất cho luồng chính để dễ kiểm soát Stop/Play
        private readonly MediaPlayer _mediaPlayer;

        // Biến cờ để kiểm soát việc lặp
        private bool _isLooping = false;

        public AudioService()
        {
            _mediaPlayer = new MediaPlayer();

            // Đăng ký sự kiện 1 lần duy nhất tại Constructor
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.MediaFailed += OnMediaFailed;
        }

        // Xử lý sự kiện khi phát xong
        private void OnMediaEnded(object? sender, EventArgs e)
        {
            if (_isLooping)
            {
                // Nếu đang bật chế độ lặp -> Tua về đầu và phát lại
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            }
            else
            {
                // Nếu không lặp -> Dừng hẳn
                _mediaPlayer.Stop();
            }
        }

        private void OnMediaFailed(object? sender, ExceptionEventArgs e)
        {
            Debug.WriteLine($"[AudioService] Error: {e.ErrorException.Message}");
        }

        /// <summary>
        /// Phát âm thanh 1 lần (Dùng cho Flashcard, Button click...)
        /// Hàm này sẽ NGẮT âm thanh đang phát trước đó.
        /// </summary>
        public void PlayAudio(string filePath)
        {
            // 1. Tắt chế độ lặp của bài cũ
            _isLooping = false;

            // 2. Dừng và đóng file cũ để tránh xung đột
            _mediaPlayer.Stop();
            _mediaPlayer.Close();

            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                Uri uri = GetUriFromPath(filePath);
                if (uri == null) return;

                Debug.WriteLine($"[AudioService] Playing: {filePath}");
                _mediaPlayer.Open(uri);
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] Play Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Phát âm thanh lặp lại (Dùng cho nhạc nền sảnh chờ...)
        /// </summary>
        public void PlayLoopingAudio(string relativePath)
        {
            // 1. Bật chế độ lặp
            _isLooping = true;

            // 2. Dừng bài cũ
            _mediaPlayer.Stop();
            _mediaPlayer.Close();

            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.WriteLine("[AudioService] Loop file not found.");
                return;
            }

            try
            {
                _mediaPlayer.Open(new Uri(fullPath, UriKind.Absolute));
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] Loop Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Dừng hoàn toàn âm thanh
        /// </summary>
        public void StopAudio()
        {
            _isLooping = false; // Quan trọng: Tắt lặp để không tự phát lại
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }

        // Helper để xử lý đường dẫn (giúp code gọn hơn)
        private Uri? GetUriFromPath(string filePath)
        {
            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(filePath);
            }

            // Xử lý đường dẫn cục bộ
            if (!Path.IsPathRooted(filePath))
            {
                string appMediaFolder = PathHelper.GetMediaFolderPath();
                filePath = Path.Combine(appMediaFolder, filePath);
            }

            if (!File.Exists(filePath)) return null;

            return new Uri(filePath, UriKind.Absolute);
        }

        // [TÙY CHỌN] PlayOneShot: Tạo ra player mới để phát đè lên (Fire-and-forget).
        // Chỉ dùng cho hiệu ứng âm thanh ngắn (SFX) cần phát chồng lên nhạc nền.
        // Nếu dùng cho Flashcard thì KHÔNG NÊN dùng hàm này.
        public void PlayOneShot(string relativePath)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(fullPath)) return;

            // Tạo instance mới để không ảnh hưởng luồng chính
            var sfxPlayer = new MediaPlayer();
            sfxPlayer.Open(new Uri(fullPath, UriKind.Absolute));

            sfxPlayer.MediaEnded += (s, e) =>
            {
                sfxPlayer.Close();
                // Không cần dispose thủ công vì WPF tự quản lý, nhưng Close() giúp giải phóng file handle
            };

            sfxPlayer.Play();
        }
    }
}