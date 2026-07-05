using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

using Application = System.Windows.Application;
using System.Linq;

namespace GamesRecap.Converters
{
    public class YearToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int year && year == 0)
            {
                var resource = Application.Current?.TryFindResource("AllYears");
                return resource as string ?? "All Years";
            }
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("This converter only supports one-way conversion.");
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                bool invert = parameter is string s && s == "invert";
                return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool invert = parameter is string s && s == "invert";
                return (v == Visibility.Visible) ^ invert;
            }
            return false;
        }
    }

    public class StringNotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;

            if (value is System.Collections.ICollection col)
                return col.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("This converter only supports one-way conversion.");
        }
    }

    public class IsCompleteDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                if (DateTime.TryParseExact(s, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("This converter only supports one-way conversion.");
        }
    }
}
