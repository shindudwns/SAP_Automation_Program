// Converters/ValueToWidthConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;

namespace SimplifyQuoter.Converters
{
    /// <summary>
    /// Converts ProgressBar.Value and ProgressBar.Maximum (and the bar’s ActualWidth)
    /// into the width of the filled portion.
    /// </summary>
    public class ValueToWidthConverter : IMultiValueConverter
    {
        /// <param name="values">
        /// values[0] = double Value
        /// values[1] = double Maximum
        /// values[2] = double ContainerWidth (e.g. ProgressBar.ActualWidth)
        /// </param>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // guard against bad inputs
            if (values == null || values.Length < 3)
                return 0.0;

            if (values[0] is double value &&
                values[1] is double max &&
                values[2] is double totalWidth &&
                max > 0)
            {
                // fraction of full width
                double fraction = Math.Max(0.0, Math.Min(1.0, value / max));
                return fraction * totalWidth;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ValueToWidthConverter does not support ConvertBack.");
        }
    }
}
