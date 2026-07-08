using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileOrganizer.Converters
{
    /// <summary>
    /// Visible when the bound value is non-null, Collapsed when null.
    /// Pass ConverterParameter="Inverse" to flip.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool inverse = parameter is string s &&
                           s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            bool visible = inverse ? isNull : !isNull;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
