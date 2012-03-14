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
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Converters
{
    public class FileSystemModelToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DirectoryModel)
            {
                return FindResource("Image_Folder_Tiny");
            }
            else if (value is FileModel)
            {
                return FindResource("Image_Document_Tiny");
            }

            return null;
        }

        private object FindResource(string resourceName)
        {
            if (Application.Current != null)
            {
                return Application.Current.Resources[resourceName];
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
