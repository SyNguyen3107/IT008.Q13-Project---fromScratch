﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IT008.Q13_Project___fromScratch.Converters
{
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Dịch ngược: true -> Collapsed, false -> Visible
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}