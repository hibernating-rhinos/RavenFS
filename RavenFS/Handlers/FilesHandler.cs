using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RavenFS.Infrastructure;
using System.Linq;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Handlers
{
	[HandlerMetadata("/files/?", "GET")]
	public class FilesHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var paging = Paging(context);

			List<FileHeader> fileHeaders = null;
			Storage.Batch(accessor =>
			{
				fileHeaders = accessor.ReadFiles(paging.Item1, paging.Item2).ToList();
			});


			return WriteArray(context, fileHeaders);
		}
	}
}