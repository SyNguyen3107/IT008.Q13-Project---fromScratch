using System;
using System.Globalization;
using System.Windows.Data;

namespace EasyFlips.Converters
{
    public class VisibilityToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value chính là biến IsPasswordVisible (bool)
            // parameter chính là chuỗi 'Hide|Show' bạn truyền từ XAML
            bool isVisible = (bool)value;
            string param = parameter as string;

            if (string.IsNullOrEmpty(param) || !param.Contains("|"))
                return isVisible ? "Hide" : "Show";

            var parts = param.Split('|');
            // Nếu IsPasswordVisible = True -> hiện phần tử đầu (Hide)
            // Nếu IsPasswordVisible = False -> hiện phần tử sau (Show)
            return isVisible ? parts[0] : parts[1];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}