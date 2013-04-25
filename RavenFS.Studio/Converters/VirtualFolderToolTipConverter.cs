using System;
using System.Globalization;
using System.Windows.Data;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Converters
{
    public class VirtualFolderToolTipConverter : IValueConverter
    {
        #region Implementation of IValueConverter

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var directoryModel = value as DirectoryModel;

	        if (directoryModel == null || !directoryModel.IsVirtual)
		        return null;
	        
			return
		        "This is a virtual folder, which will disappear at the end of this session, unless you upload something into it.";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
