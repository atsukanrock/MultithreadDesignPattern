using System;
using System.Globalization;
using System.Windows.Data;

namespace ImageProcessor.Admin.Converters
{
    internal class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool negated;
            try
            {
                negated = System.Convert.ToBoolean(parameter);
            }
            catch
            {
                negated = false;
            }
            return (value != null) != negated;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}