using System;
using System.Globalization;
using System.Windows;
using RavenFS.Studio.External.MultiBinding;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Converters
{
    public class AsyncOperationStatusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
	        if (values.Length < 2 || !(values[0] is AsyncOperationStatus))
		        return DependencyProperty.UnsetValue;

	        var status = (AsyncOperationStatus)values[0];
            var error = values[1] as string;

            return status == AsyncOperationStatus.Error ? "Error: " + (error ?? "") : status.ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
