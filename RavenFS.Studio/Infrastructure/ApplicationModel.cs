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
		    var documentUri = HtmlPage.Document.DocumentUri;
		    if (documentUri.Scheme == "file")
			{
				return "http://localhost";
			}
		    var path = documentUri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
            var lastIndexOfUI = path.LastIndexOf("ui");
            if (lastIndexOfUI != -1)
			{
                path = path.Substring(0, lastIndexOfUI);
			}

		    var uriBuilder = new UriBuilder(documentUri.Scheme, documentUri.DnsSafeHost, documentUri.Port, path);

		    return uriBuilder.Uri.ToString();
		}

		public static Uri GetFileUrl(string fileName)
		{
			return new Uri(DetermineUri()+"/files/"+fileName);
		}
	}
}
