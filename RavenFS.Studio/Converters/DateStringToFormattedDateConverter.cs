using System;
using System.Globalization;
using System.Windows.Data;

namespace RavenFS.Studio.Converters
{
    public class DateStringToFormattedDateConverter : IValueConverter
    {
        public bool ConvertToUniversalTime { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime dateTime;
	        if (!DateTime.TryParse(value as string, out dateTime))
		        return value;
	        
			if (ConvertToUniversalTime)
		        dateTime = dateTime.ToUniversalTime();

	        return dateTime.ToString(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
