using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Interop;
using RavenFS.Client;

namespace RavenFS.Studio.Infrastructure
{
	public class ApplicationModel
	{
		static ApplicationModel()
		{
            if (DesignerProperties.IsInDesignTool)
            {
                // we don't want our pages crashing when loaded in the designer
                return;
            }

			Client = new RavenFileSystemClient(DetermineUri());
		}

		public static RavenFileSystemClient Client { get; private set; }

		private static string DetermineUri()
		{
            // check for a server UI in the InitParams of the Silverlight Host
            // this allows us to configure a debug page on the local file system that we can load in
            // SilverlightSpy to inspect the XAP
            if (Application.Current.Host.InitParams.ContainsKey("ServerUri"))
            {
                return Application.Current.Host.InitParams["ServerUri"];
            }

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
