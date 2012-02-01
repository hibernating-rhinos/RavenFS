using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/sync/version", "GET")]
    public class SyncVersionHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            return WriteJson(context, new
            {
                Version = 1,
            });
        }
    }
}