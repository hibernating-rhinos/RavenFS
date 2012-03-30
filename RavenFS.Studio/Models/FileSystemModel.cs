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

        public string Name { get; private set; }

        public string Folder
        {
            get; private set;
        }

        public string FullPath
        {
            get { return fullPath; }
            set
            {
                fullPath = value;
                var lastSlash = fullPath.LastIndexOf("/", StringComparison.InvariantCulture);

                if (lastSlash > 0)
                {
                    Name = fullPath.Substring(lastSlash + 1);
                    Folder = fullPath.Substring(0, lastSlash);
                }
                else
                {
                    Name = fullPath;
                    Folder = "/";
                }
            }
        }
    }
}
