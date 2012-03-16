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
using RavenFS.Client;

namespace RavenFS.Studio.Models
{
    public class FileModel : FileSystemModel
    {
        public string FormattedTotalSize { get; set; }
        public NameValueCollection Metadata { get; set; }
    }
}
