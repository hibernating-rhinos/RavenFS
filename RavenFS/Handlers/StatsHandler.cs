using System;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/stats$", "GET")]
	public class StatsHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var count = 0;
			Storage.Batch(accessor =>
			{
				count = accessor.GetFileCount();
			});
			return WriteJson(context, new
			{
				FileCount = count
			});
		}
	}
}