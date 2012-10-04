using System;
using System.Collections.Generic;
using System.Net;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Web.Http.SelfHost;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Tests.Tools;

namespace RavenFS.Tests
{
	using System.Globalization;
	using System.Linq;
	using RavenFS.Config;
	using Storage;

	public abstract class MultiHostTestBase : WithNLog, IDisposable
	{
		public static readonly int[] Ports = { 19079, 19081 };

		private readonly IList<IDisposable> disposables = new List<IDisposable>();

		protected const string UrlBase = "http://localhost:";

		protected MultiHostTestBase()
		{
			foreach (var port in Ports)
			{
				StartServerInstance(port);
			}
		}

		protected void StartServerInstance(int port)
		{
			NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(port);
			HttpSelfHostConfiguration config = null;
			Task.Factory.StartNew(() => // initialize in MTA thread
			                      	{
			                      		config = new HttpSelfHostConfiguration(ServerAddress(port))
			                      		         	{
			                      		         		MaxReceivedMessageSize = Int64.MaxValue,
			                      		         		TransferMode = TransferMode.Streamed
			                      		         	};

			                      		var configuration = new InMemoryConfiguration();
										configuration.Initialize();
			                      		configuration.DataDirectory = "~/" + port;

			                      		var ravenFileSystem = new RavenFileSystem(new RavenFileSystemConfiguration());
			                      		ravenFileSystem.Start(config);
			                      		disposables.Add(ravenFileSystem);
			                      	})
				.Wait();

			var server = new HttpSelfHostServer(config);
			server.OpenAsync().Wait();

			disposables.Add(server);
		}

		protected static string ServerAddress(int port)
		{
			return UrlBase + port + "/";
		}

		protected RavenFileSystemClient NewClient(int index)
		{
			return new RavenFileSystemClient(ServerAddress(Ports[index]));
		}

		protected RavenFileSystem GetRavenFileSystem(int index)
		{
			return
				disposables.OfType<RavenFileSystem>().First(
					x => x.Configuration.DataDirectory.EndsWith(Ports[index].ToString(CultureInfo.InvariantCulture)));
		}

		#region IDisposable Members

		public virtual void Dispose()
		{
			foreach (var disposable in disposables)
			{
				var httpSelfHostServer = disposable as HttpSelfHostServer;
				if (httpSelfHostServer != null)
					httpSelfHostServer.CloseAsync().Wait();
				disposable.Dispose();
			}
		}

		#endregion
	}
}
