using System;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Converters
{
    public class DateStringToFormattedDateConverter : IValueConverter
    {
        public bool ConvertToUniversalTime { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime dateTime;
            if (!DateTime.TryParse(value as string, out dateTime))
            {
                return value;
            }
            else
            {
                if (ConvertToUniversalTime)
                {
                    dateTime = dateTime.ToUniversalTime();
                }

                return dateTime.ToString(culture);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
