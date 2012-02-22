using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Raven.Abstractions.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/files/(.+)", "POST")]
    public class SimplePostHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
			var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

            var headers = context.Request.Headers.FilterHeaders();
            headers.UpdateLastModified();
            try
            {
                Storage.Batch(accessor => accessor.UpdateFileMetadata(filename, headers));
                Search.Index(filename, headers);
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = 404;
            }
            return Completed;
        }
    }
}