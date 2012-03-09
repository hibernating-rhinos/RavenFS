﻿using System.Collections.Generic;
using RavenFS.Client;

namespace RavenFS.Controllers
{
	public class FoldersController : RavenController
	{
		public IEnumerable<string> Subdirectories(string directory)
		{
			return Search.GetTermsFor("__directory", directory, Paging.PageSize);
		}
	}
}