using System.ComponentModel;
using System.ServiceProcess;


namespace RavenFS.Server
{
	[RunInstaller(true)]
	public partial class ProjectInstaller : System.Configuration.Install.Installer
	{
		internal static string SERVICE_NAME = "RavenFS";

		public ProjectInstaller()
		{
			InitializeComponent();

			ServiceName = SERVICE_NAME;

			this.serviceInstaller1.StartType = ServiceStartMode.Automatic;

			this.serviceProcessInstaller1.Account = ServiceAccount.LocalSystem;
		}

		public string ServiceName
		{
			get
			{
				return serviceInstaller1.DisplayName;
			}
			set
			{
				serviceInstaller1.DisplayName = value;
				serviceInstaller1.ServiceName = value;
			}
		}
	}
}
