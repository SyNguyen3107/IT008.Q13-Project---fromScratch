using System;
using System.IO;

namespace EasyFlips.Helpers
{
    public static class PathHelper
    {
        // Trả về: C:\Users\<CurrentUsers>\AppData\Roaming\EasyFlips\Media
        public static string GetMediaFolderPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mediaPath = Path.Combine(appData, "EasyFlips", "Media");

            // Tạo thư mục nếu chưa có
            if (!Directory.Exists(mediaPath))
            {
                Directory.CreateDirectory(mediaPath);
            }
            return mediaPath;
        }

        // Hàm tiện ích: Đưa vào "cat.png" -> Trả về đường dẫn full
        public static string GetFullPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            // Nếu database lỡ lưu đường dẫn tuyệt đối cũ -> Cắt lấy tên file
            string nameOnly = Path.GetFileName(fileName);

            return Path.Combine(GetMediaFolderPath(), nameOnly);
        }
    }
}