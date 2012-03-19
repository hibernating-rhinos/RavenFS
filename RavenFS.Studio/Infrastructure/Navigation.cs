using System;
using System.Net;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Infrastructure
{
    public static class Navigation
    {
        public static void Download(string filePath)
        {
            var url = ApplicationModel.Current.GetFileUrl(filePath);
            HtmlPage.Window.Navigate(url);
        }

        public static void Folder(string folderPath)
        {
            UrlUtil.Navigate("/files" + folderPath);
        }
    }
}
