using System;
using System.Windows.Browser;
using RavenFS.Client;

namespace RavenFS.Studio.Infrastructure
{
	public class ApplicationModel
	{
		static ApplicationModel()
		{
			Client = new RavenFileSystemClient(DetermineUri());
		}

		public static RavenFileSystemClient Client { get; private set; }

		private static string DetermineUri()
		{
			if (HtmlPage.Document.DocumentUri.Scheme == "file")
			{
				return "http://ipv4.fiddler";
			}
			var localPath = HtmlPage.Document.DocumentUri.LocalPath;
			var lastIndexOfRaven = localPath.LastIndexOf("/raven/");
			if (lastIndexOfRaven != -1)
			{
				localPath = localPath.Substring(0, lastIndexOfRaven);
			}
			return new UriBuilder(HtmlPage.Document.DocumentUri)
			{
				Path = localPath
			}.Uri.ToString();
		}

		public static Uri GetFileUrl(string fileName)
		{
			return new Uri(DetermineUri()+"/files/"+fileName);
		}
	}
}
