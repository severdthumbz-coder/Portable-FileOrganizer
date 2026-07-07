using System;
using System.Globalization;
using System.Windows.Data;

namespace FileOrganizer.Converters
{
    /// <summary>
    /// Converts boolean IsExpanded to expand/collapse symbol
    /// </summary>
    public class ExpandCollapseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶";
            }
            return "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
