using System;
using System.Web.Routing;
using System.Web.Http;
using RavenFS.Web.Infrastructure;

namespace RavenFS.Web
{
	public class Global : System.Web.HttpApplication
	{
		private static RavenFileSystem ravenFileSystem;

		protected void Application_Start(object sender, EventArgs e)
		{
			ravenFileSystem = new RavenFileSystem();

			ravenFileSystem.Start(GlobalConfiguration.Configuration);
		}

		protected void Application_End(object sender, EventArgs e)
		{
			using(ravenFileSystem)
			{
				
			}
		}
	}
}