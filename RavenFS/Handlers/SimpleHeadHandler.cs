using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Raven.Abstractions.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)", "HEAD")]
	public class SimpleHeadHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{

			var filename = Url.Match(context.Request.Url.AbsolutePath).Groups[1].Value;
			FileInformation fileInformation = null;
			try
			{
				Storage.Batch(accessor => fileInformation = accessor.GetFile(filename, 0, 0));
			}
			catch (FileNotFoundException)
			{
				context.Response.StatusCode = 404;

				return Completed;
			}

			MetadataExtensions.AddHeaders(context, fileInformation);

			return Completed;
		}
	}
}