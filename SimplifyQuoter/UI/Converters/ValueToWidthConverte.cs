// Converters/ValueToWidthConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace SimplifyQuoter.Converters
{
    /// <summary>
    /// Given two bound values:
    ///   values[0] = the current progress value (0–100),
    ///   values[1] = the total pixel width to fill (e.g. 300),
    /// returns value/100 * totalWidth.
    /// </summary>
    public class ValueToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return 0.0;

            // first binding: the ProgressBar.Value (should be double)
            if (!(values[0] is double current))
                return 0.0;

            // second binding: the available total width
            double totalWidth;
            if (values[1] is double d)
            {
                totalWidth = d;
            }
            else if (values[1] is string s && double.TryParse(s, out var parsed))
            {
                totalWidth = parsed;
            }
            else
            {
                return 0.0;
            }

            // clamp 0–100 then scale
            var pct = Math.Max(0, Math.Min(100, current)) / 100.0;
            return pct * totalWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ValueToWidthConverter is OneWay only");
        }
    }
}
