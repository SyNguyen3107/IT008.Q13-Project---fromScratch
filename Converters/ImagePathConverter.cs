using EasyFlips.Helpers;
using System;
using System.Diagnostics;
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

            Debug.WriteLine($"ImagePathConverter: Converting value: {value}");
            string fileName = value as string;
            if (string.IsNullOrEmpty(fileName)) return null;

            try
            {
                // Nếu là URL http/https thì load trực tiếp
                if (fileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fileName, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }

                // Nếu là file cục bộ
                string fullPath = PathHelper.GetFullPath(fileName);
                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
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