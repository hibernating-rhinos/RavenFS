using System;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace RavenFS
{
	using System.IO;
	using System.Xml;
	using Config;
	using NLog.Config;
	using Util;

	public class Global : System.Web.HttpApplication
	{
		private static RavenFileSystem ravenFileSystem;

		protected void Application_Start(object sender, EventArgs e)
		{
			HttpEndpointRegistration.RegisterHttpEndpointTarget();
			ConfigureLogging();

			ravenFileSystem = new RavenFileSystem(new RavenFileSystemConfiguration());

			ravenFileSystem.Start(GlobalConfiguration.Configuration);

            // turn this on so we don't a conflict between the /search endpoint handled by the SearchController
            // and the Search folder.
		    RouteTable.Routes.RouteExistingFiles = true;
		}

		protected void Application_End(object sender, EventArgs e)
		{
			using(ravenFileSystem)
			{
				
			}
		}

		private static void ConfigureLogging()
		{
			var nlogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
			if (File.Exists(nlogPath))
				return;// that overrides the default config

			using (var stream = typeof(Global).Assembly.GetManifestResourceStream("RavenFS.DefaultLogging.config"))
			using (var reader = XmlReader.Create(stream))
			{
				NLog.LogManager.Configuration = new XmlLoggingConfiguration(reader, "default-config");
			}
		}
	}
}