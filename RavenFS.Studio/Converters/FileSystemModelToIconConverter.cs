using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Converters
{
    public class FileSystemModelToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
	        if (value == null)
		        return null;

	        if (value is DirectoryModel)
            {
                var directoryModel = value as DirectoryModel;
                return directoryModel.IsVirtual ? FindResource("Image_VirtualFolder_Tiny") : FindResource("Image_Folder_Tiny");
            }

	        if (value is FileModel)
		        return FindResource("Image_Document_Tiny");

	        return null;
        }

        private object FindResource(string resourceName)
        {
	        return Application.Current != null ? Application.Current.Resources[resourceName] : null;
        }

	    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
