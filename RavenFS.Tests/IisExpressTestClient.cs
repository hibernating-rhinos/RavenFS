using System;
using System.Net;
using RavenFS.Client;

namespace RavenFS.Tests
{
	public class IisExpressTestClient : IDisposable
	{
		public static int Port = 8084;

		private IisExpressDriver _iisExpress;

		protected WebClient webClient;

		static IisExpressTestClient()
		{
			try
			{
				new Uri("http://localhost/?query=Customer:Northwind%20AND%20Preferred:True");
			}
			catch (Exception)
			{
			}
		}

		protected HttpWebRequest CreateWebRequest(string url)
		{
			return (HttpWebRequest)WebRequest.Create(_iisExpress.Url + url);
		}

		protected RavenFileSystemClient NewClient()
		{
			return new RavenFileSystemClient(_iisExpress.Url);
		}

		#region IDisposable Members

		public void Dispose()
		{
			if (_iisExpress != null)
			{
				_iisExpress.Dispose();
				_iisExpress = null;
			}
		}

		#endregion

		public IisExpressTestClient()
		{
			_iisExpress = new IisExpressDriver();
			_iisExpress.Start(IisDeploymentUtil.DeployWebProjectToTestDirectory(), 8084);
			webClient = new WebClient
			{
				BaseAddress = _iisExpress.Url
			};
		}
	}
}