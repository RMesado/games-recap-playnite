using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace GamesRecap.Converters
{
    public class TagIconToGeometryConverter : IValueConverter
    {
        private static readonly ResourceDictionary BadgeIcons = new ResourceDictionary
        {
            Source = new Uri("/GamesRecap;component/Resources/BadgeIcons.xaml", UriKind.RelativeOrAbsolute)
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconName && !string.IsNullOrEmpty(iconName))
            {
                string key = "Icon" + ToPascalCase(iconName);
                if (BadgeIcons.Contains(key))
                    return BadgeIcons[key];
            }
            return BadgeIcons["IconTagDefault"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string ToPascalCase(string kebab)
        {
            var parts = kebab.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join("", parts);
        }
    }
}
