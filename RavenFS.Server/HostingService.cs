using System;
using System.ServiceModel;
using System.ServiceProcess;
using System.Web.Http.SelfHost;

namespace RavenFS.Server
{
	using Config;

	partial class HostingService : ServiceBase
	{
		private RavenFileSystem ravenFileSystem;
		private HttpSelfHostConfiguration config;
		private HttpSelfHostServer server;

		public HostingService()
		{
			InitializeComponent();
		}

		public RavenFileSystemConfiguration Configuration { get; set; }

		protected override void OnStart(string[] args)
		{
			Start();
		}

		public void Start()
		{
			ravenFileSystem = new RavenFileSystem(Configuration);
			config = new HttpSelfHostConfiguration(Configuration.ServerUrl)
			{
				MaxReceivedMessageSize = Int64.MaxValue,
				TransferMode = TransferMode.Streamed,
				ReceiveTimeout = TimeSpan.MaxValue,
				SendTimeout = TimeSpan.MaxValue,
			};
			ravenFileSystem.Start(config);
			server = new HttpSelfHostServer(config);
			server.OpenAsync().Wait();
		}

		protected override void OnStop()
		{
			DoStop();
		}

		public void DoStop()
		{
			server.CloseAsync().Wait();
			config.Dispose();
			ravenFileSystem.Dispose();
		}
	}
}
