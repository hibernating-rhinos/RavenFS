using System;
using System.Net;
using RavenFS.Client;

namespace RavenFS.Tests
{
	public class IisExpressTestClient : IDisposable
	{
		public static int Port = 8084;

		private IisExpressDriver iisExpress;

		protected WebClient WebClient;

		static IisExpressTestClient()
		{
			try
			{
				new Uri("http://localhost/?query=Customer:Northwind%20AND%20Preferred:True");
			}
			catch
			{
			}
		}

		protected HttpWebRequest CreateWebRequest(string url)
		{
			return (HttpWebRequest)WebRequest.Create(iisExpress.Url + url);
		}

		protected RavenFileSystemClient NewClient()
		{
			return new RavenFileSystemClient(iisExpress.Url);
		}

		#region IDisposable Members

		public void Dispose()
		{
			if (iisExpress != null)
			{
				iisExpress.Dispose();
				iisExpress = null;
			}
		}

		#endregion

		public IisExpressTestClient()
		{
			iisExpress = new IisExpressDriver();
			iisExpress.Start(IisDeploymentUtil.DeployWebProjectToTestDirectory(Port), Port);
			WebClient = new WebClient
			{
				BaseAddress = iisExpress.Url
			};
		}
	}
}