using System;
using System.Globalization;
using System.Windows.Data;
using RavenFS.Client;

namespace RavenFS.Studio.Converters
{
	public class NameValueCollectionToMultiLineStringConverter :  IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var data = value as NameValueCollection;
			if (data == null)
				return "";

			string stringData = "";

			foreach (var item in data.Values)
			{
				stringData += item + "\n";
			}

			return stringData;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
