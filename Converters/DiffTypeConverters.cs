using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DiffPlex.DiffBuilder.Model;

namespace EasyFlips.Converters
{
    // 1. Brush converter
    public class DiffTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChangeType type)
            {
                return type switch
                {
                    ChangeType.Unchanged => Brushes.Black,   // Đúng
                    ChangeType.Inserted => Brushes.Green,   // Thiếu
                    ChangeType.Deleted => Brushes.Red,     // Thừa
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // 2. FontWeight converter
    public class DiffTypeToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChangeType type)
            {
                return type == ChangeType.Deleted ? FontWeights.Bold : FontWeights.Normal;
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // 3. TextDecoration converter
    public class DiffTypeToTextDecorationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChangeType type)
            {
                return type == ChangeType.Deleted ? TextDecorations.Strikethrough : null;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
