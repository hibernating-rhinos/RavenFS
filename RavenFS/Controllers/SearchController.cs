using System.Collections.Generic;
using System.Linq;
using RavenFS.Storage;

namespace RavenFS.Controllers
{
	public class SearchController : RavenController
	{
		public SearchResults Get(string query, string[] sort)
		{
			int results;
			var keys = Search.Query(query, sort, Paging.Start, Paging.PageSize, out results);

			var list = new List<FileHeader>();

			Storage.Batch(accessor => list.AddRange(keys.Select(accessor.ReadFile).Where(x => x != null)));

			return new SearchResults
			{
				Start = Paging.Start,
				PageSize = Paging.PageSize,
				Files = list,
				FileCount = results
			};
		}
	}
}