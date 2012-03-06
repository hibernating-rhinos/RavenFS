using System;
using System.Web.Routing;
using System.Web.Http;
using RavenFS.Web.Infrastructure;

namespace RavenFS.Web
{
	public class Global : System.Web.HttpApplication
	{
		protected void Application_Start(object sender, EventArgs e)
		{
			RavenFileSystem.Start(GlobalConfiguration.Configuration);
		}

		protected void Application_End(object sender, EventArgs e)
		{
			RavenFileSystem.Stop();
		}
	}
}