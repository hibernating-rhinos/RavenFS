using System;
using System.Globalization;
using System.Windows.Data;

namespace RavenFS.Studio.Converters
{
    public class BooleanConverter : IValueConverter
    {
        public object ValueWhenTrue { get; set; }

        public object ValueWhenFalse { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? ValueWhenTrue : ValueWhenFalse;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
