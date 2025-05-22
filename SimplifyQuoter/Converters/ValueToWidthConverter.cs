// File: Converters/ValueToWidthConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace SimplifyQuoter.Converters
{
    public class ValueToWidthConverter : IMultiValueConverter
    {
        // values[0] = current progress (0–100)
        // values[1] = container width (ActualWidth)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 ||
                !(values[0] is double progress) ||
                !(values[1] is double totalWidth))
                return 0.0;

            progress = Math.Max(0, Math.Min(100, progress));
            return totalWidth * (progress / 100.0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
