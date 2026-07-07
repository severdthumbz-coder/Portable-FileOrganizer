using System;
using System.Globalization;
using System.Windows.Data;

namespace FileOrganizer.Converters
{
    /// <summary>
    /// Converts progress value to width for the progress bar indicator
    /// </summary>
    public class ProgressBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3)
                return 0.0;

            // values[0] = Current Value
            // values[1] = Container ActualWidth
            // values[2] = Maximum Value

            if (values[0] is double currentValue &&
                values[1] is double containerWidth &&
                values[2] is double maximum)
            {
                if (maximum == 0)
                    return 0.0;

                var percentage = currentValue / maximum;
                var width = containerWidth * percentage;

                return Math.Max(0, Math.Min(width, containerWidth));
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
