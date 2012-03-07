using System;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.External.MultiBinding;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Converters
{
    public class AsyncOperationStatusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is AsyncOperationStatus) || !(values[1] is string))
            {
                return DependencyProperty.UnsetValue;
            }

            var status = (AsyncOperationStatus)values[0];
            var error = (string) values[1];

            return status == AsyncOperationStatus.Error ? "Error: " + error : status.ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
