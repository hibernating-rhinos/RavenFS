using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Rdc;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/signatures/(.+)", "GET")]
    public class RdcSignatureHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.BufferOutput = false;
            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

            var localRdcAccess = new LocalRdcAccess(Storage, FileAccess, SigGenerator);
            var signatureInfo = localRdcAccess.GetSignatureInfo(fileName);
            var sigFile = signatureInfo.OpenRead();

            context.Response.AddHeader("Content-Length", sigFile.Length.ToString());
            context.Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);


            return sigFile.CopyToAsync(context.Response.OutputStream)
                .ContinueWith(task => sigFile.Dispose());
        }
    }
}