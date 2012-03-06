using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http.SelfHost;
using RavenFS.Client;
using RavenFS.Web;

namespace RavenFS.Tests
{
	public class WebApiTest : IDisposable
	{
		private HttpSelfHostConfiguration config;
		private HttpSelfHostServer server;
		private const string Url = "http://localhost:9079";
		protected WebClient WebClient;

		static WebApiTest()
		{
			try
			{
				new Uri("http://localhost/?query=Customer:Northwind%20AND%20Preferred:True");
			}
			catch
			{
			}
		}

		public WebApiTest()
		{
			NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(9079);
			Task.Factory.StartNew(() => // initialize in MTA thread
			{
				config = new HttpSelfHostConfiguration(Url);

				RavenFileSystem.Start(config);

				server = new HttpSelfHostServer(config);
				return server.OpenAsync();
			})
				.Unwrap()
				.Wait();
			
			WebClient = new WebClient
			{
				BaseAddress = Url
			};
		}

		protected RavenFileSystemClient NewClient()
		{
			return new RavenFileSystemClient(Url);
		}

		public void Dispose()
		{
			server.Dispose();
			config.Dispose();
			RavenFileSystem.Stop();
		}
	}
}