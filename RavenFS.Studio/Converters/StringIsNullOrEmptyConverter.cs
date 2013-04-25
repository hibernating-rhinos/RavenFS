using System;
using System.Globalization;
using System.Windows.Data;

namespace RavenFS.Studio.Converters
{
    public class NullConverter : IValueConverter
    {
        public object ValueWhenNull { get; set; }

        public object ValueWhenNotNull { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? ValueWhenNull : ValueWhenNotNull;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
