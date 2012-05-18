using System.Collections.Generic;
using System.Web.Http;
using RavenFS.Client;
using System.Linq;

namespace RavenFS.Controllers
{
	public class FoldersController : RavenController
	{
		[AcceptVerbs("GET")]
		public IEnumerable<string> Subdirectories(string directory)
		{
			var add = directory == null ? 0 : 1;
			directory = "/" + directory;
			var nesting = directory.Count(ch => ch == '/') + add;
			return Search.GetTermsFor("__directory", directory)
				.Where(subDir =>
				{
					if (subDir.StartsWith(directory) == false)
						return false;

					return nesting == subDir.Count(ch => ch == '/');
				})
				.Skip(Paging.Start)
				.Take(Paging.PageSize);
		}
	}
}