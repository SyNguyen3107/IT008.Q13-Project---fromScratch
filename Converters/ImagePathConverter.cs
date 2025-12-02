using EasyFlips.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace EasyFlips.Converters
{
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string fileName = value as string;
            if (string.IsNullOrEmpty(fileName)) return null;

            try
            {
                // Lấy đường dẫn đầy đủ dựa trên máy tính hiện tại
                string fullPath = PathHelper.GetFullPath(fileName);

                if (File.Exists(fullPath))
                {
                    // Load ảnh bitmap (Dùng CacheOption để không khóa file ảnh)
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch
            {
                // Nếu lỗi load ảnh thì trả về null hoặc ảnh mặc định
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}