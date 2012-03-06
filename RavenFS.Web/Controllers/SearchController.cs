using System.Collections.Generic;
using System.Linq;
using RavenFS.Storage;

namespace RavenFS.Web.Controllers
{
	public class SearchController : RavenController
	{
		public List<FileHeader> Get()
		{
			var query = QueryString["query"];
			var keys = Search.Query(query, Paging.Start, Paging.PageSize);

			var list = new List<FileHeader>();

			Storage.Batch(accessor => list.AddRange(keys.Select(accessor.ReadFile)));

			return list;
		}
	}
}