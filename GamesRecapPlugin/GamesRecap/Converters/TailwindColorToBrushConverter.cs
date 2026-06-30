using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GamesRecap.Converters
{
    public class TailwindColorToBrushConverter : IValueConverter
    {
        private static readonly Dictionary<string, Color> ColorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "text-sky-400", Color.FromRgb(0x38, 0xbd, 0xf8) },
            { "text-emerald-400", Color.FromRgb(0x34, 0xd3, 0x99) },
            { "text-lime-400", Color.FromRgb(0xa3, 0xe6, 0x35) },
            { "text-violet-400", Color.FromRgb(0xa7, 0x8b, 0xfa) },
            { "text-red-500", Color.FromRgb(0xef, 0x44, 0x44) },
            { "text-amber-400", Color.FromRgb(0xfb, 0xbf, 0x24) },
            { "text-pink-400", Color.FromRgb(0xf4, 0x72, 0x81) },
            { "text-cyan-400", Color.FromRgb(0x22, 0xd3, 0xee) },
            { "text-orange-400", Color.FromRgb(0xfb, 0x92, 0x3c) },
            { "text-teal-400", Color.FromRgb(0x2d, 0xd4, 0xbf) },
            { "text-blue-400", Color.FromRgb(0x60, 0xa5, 0xfa) },
            { "text-indigo-400", Color.FromRgb(0x81, 0x8c, 0xf8) },
            { "text-fuchsia-400", Color.FromRgb(0xe8, 0x79, 0xf9) },
            { "text-rose-400", Color.FromRgb(0xfb, 0x71, 0x85) },
            { "text-yellow-400", Color.FromRgb(0xfa, 0xce, 0x22) },
            { "text-green-400", Color.FromRgb(0x4a, 0xde, 0x80) },
            { "text-white/90", Color.FromRgb(0xe6, 0xe6, 0xe6) },
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tailwindColor && !string.IsNullOrEmpty(tailwindColor))
            {
                if (ColorMap.TryGetValue(tailwindColor, out var color))
                    return new SolidColorBrush(color);
            }
            return new SolidColorBrush(Color.FromRgb(0xcc, 0xcc, 0xcc));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("This converter only supports one-way conversion.");
        }
    }
}
