using System;
using System.Globalization;
using System.Windows.Data;

namespace RavenFS.Studio.Converters
{
    public class IntegerToFormattedStringConverter : IValueConverter
    {
        public string FormatWhenZero { get; set; }
        public string FormatWhenOne { get; set; }
        public string FormatWhenMany { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var integerValue = System.Convert.ToInt32(value);

	        if (integerValue == 0)
		        return string.Format(FormatWhenZero, integerValue);
	        
			if (integerValue == 1)
		        return string.Format(FormatWhenOne, integerValue);
	        
			return string.Format(FormatWhenMany, integerValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
