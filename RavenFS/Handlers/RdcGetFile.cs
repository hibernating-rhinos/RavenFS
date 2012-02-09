using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/files/(.+)", "GET")]
    public class RdcFileHandler : AbstractAsyncHandler
    {
        private static readonly Regex startRange = new Regex(@"^bytes=(\d+)-$", RegexOptions.Compiled);

        protected override Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.BufferOutput = false;
            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

            var signatureInfo = new SignatureInfo(FileAccess, fileName);
            var sigFile = signatureInfo.OpenRead();

            context.Response.AddHeader("Content-Length", sigFile.Length.ToString());
            context.Response.AddHeader("Content-Disposition", "attachment; filename=" + sigFile);


            return sigFile.CopyToAsync(context.Response.OutputStream)
                .ContinueWith(task => sigFile.Dispose());
        }

        private static long? GetRangesFromHeaders(HttpContext context, string name)
        {
            var literal = context.Request.Headers[name];
            if (string.IsNullOrEmpty(literal))
                return null;

            var match = startRange.Match(literal);

            if (match.Success == false)
                return null;

            long result;
            if (long.TryParse(match.Groups[1].Value, out result) == false)
                return null;

            return result;
        }
    }
}