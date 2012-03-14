using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Models
{
    public class FileSystemModel
    {
        private string fullPath;
        private string name;

        public string Name
        {
            get { return name; }
            private set { name = value; }
        }

        public string FullPath
        {
            get { return fullPath; }
            set
            {
                fullPath = value;
                name = fullPath.Substring(fullPath.LastIndexOf("/", StringComparison.InvariantCulture) + 1);
            }
        }
    }
}
