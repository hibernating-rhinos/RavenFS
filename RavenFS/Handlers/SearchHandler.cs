using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/search/?","GET")]
	public class SearchHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var paging = Paging(context);
			var query = context.Request.QueryString["query"];
			var keys = Search.Query(query, paging.Item1, paging.Item2);

			var list = new List<FileHeader>();

			Storage.Batch(accessor => list.AddRange(keys.Select(accessor.ReadFile)));

			return WriteArray(context, list);
		}
	}
}