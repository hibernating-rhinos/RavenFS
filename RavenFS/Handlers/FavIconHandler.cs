using System;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/favicon.ico$", "GET")]
	public class FavIconHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			context.Response.Status = "Not Found";
			context.Response.StatusCode = 404;
			return Completed;
		}
	}
}