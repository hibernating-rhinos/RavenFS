using System.Windows.Browser;
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
