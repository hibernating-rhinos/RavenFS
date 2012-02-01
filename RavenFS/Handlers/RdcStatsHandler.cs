using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/stats", "GET")]
    public class RdcStatsHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            return WriteJson(context, new
            {
                Version = Rdc.Wrapper.Msrdc.Version,
            });
        }
    }
}