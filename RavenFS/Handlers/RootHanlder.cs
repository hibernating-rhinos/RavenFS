using System;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/$", "GET")]
	public class RootHanlder : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			context.Response.Redirect("/files");
			return Completed;
		}
	}
}