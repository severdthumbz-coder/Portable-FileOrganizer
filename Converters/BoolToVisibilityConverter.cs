using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileOrganizer.Converters
{
    /// <summary>
    /// Converts bool to Visibility with inversion support
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            string param = parameter?.ToString();
            bool invert = param == "Inverse" || param == "Inverted";

            if (invert)
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;
            string param = parameter?.ToString();
            bool invert = param == "Inverse" || param == "Inverted";

            return invert ? !isVisible : isVisible;
        }
    }
}
