using System.Collections.Generic;

namespace RavenFS.Storage
{
	public class SearchResults
	{
		public List<FileHeader> Files { get; set; }
		public int FileCount { get; set; }
		public int Start { get; set; }
		public int PageSize { get; set; }
	}
}