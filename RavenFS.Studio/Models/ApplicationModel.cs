using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Browser;
using RavenFS.Client;

namespace RavenFS.Studio.Models
{
	public class ApplicationModel
	{
        public static readonly ApplicationModel Current = new ApplicationModel();

		public ApplicationModel()
		{
			Client = new RavenFileSystemClient(DetermineUri());
		    AsyncOperations = new AsyncOperationsModel();
		    State = new ApplicationState();
		}

	    public ApplicationState State { get; private set; }

	    public AsyncOperationsModel AsyncOperations { get; private set; }

	    public RavenFileSystemClient Client { get; private set; }

       

		private static string DetermineUri()
		{
            if (DesignerProperties.IsInDesignTool)
            {
                return string.Empty;
            }

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
				return "http://localhost:9090";
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

		public Uri GetFileUrl(string fileName)
		{
			return new Uri(Client.ServerUrl+"/files/"+fileName);
		}
	}
}
