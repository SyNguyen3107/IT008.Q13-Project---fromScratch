using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EasyFlips.Converters
{
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Kiểm tra null hoặc empty cho cả string và object
            bool isNullOrEmpty;
            
            if (value == null)
            {
                isNullOrEmpty = true;
            }
            else if (value is string str)
            {
                isNullOrEmpty = string.IsNullOrEmpty(str);
            }
            else
            {
                // Object không null
                isNullOrEmpty = false;
            }

            // Xử lý tham số Inverse
            bool inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

            if (inverse)
            {
                // Inverse: null → Visible, not null → Collapsed
                return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Normal: null → Collapsed, not null → Visible
                return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}