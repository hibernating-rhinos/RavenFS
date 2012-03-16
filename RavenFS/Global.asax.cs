using System;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace RavenFS
{
	public class Global : System.Web.HttpApplication
	{
		private static RavenFileSystem ravenFileSystem;

		protected void Application_Start(object sender, EventArgs e)
		{
			ravenFileSystem = new RavenFileSystem();

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
	}
}