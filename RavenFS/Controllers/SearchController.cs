using System.Collections.Generic;
using System.Linq;
using RavenFS.Storage;

namespace RavenFS.Controllers
{
	public class SearchController : RavenController
	{
		public List<FileHeader> Get(string query, string[] sort)
		{
			var keys = Search.Query(query, sort, Paging.Start, Paging.PageSize);

			var list = new List<FileHeader>();

			Storage.Batch(accessor => list.AddRange(keys.Select(accessor.ReadFile)));

			return list;
		}
	}
}