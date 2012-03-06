using System;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)", "DELETE")]
	public class SimpleDeleteHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

			Search.Delete(filename);
			Storage.Batch(accessor => accessor.Delete(filename));

			context.Response.StatusCode = 204;

			return Completed;
		}
	}
}