﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IT008.Q13_Project___fromScratch.Converters
{
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Nếu giá trị là null hoặc rỗng, ẩn nó đi (Collapsed)
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}