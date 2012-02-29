using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Extensions;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/ui/?(.*)$", "GET")]
    public class WebUIHandler : AbstractAsyncHandler
    {
        private const string SilverlightXAP = "RavenFS.Studio.xap";

        protected override Task ProcessRequestAsync(HttpContext context)
        {
            var requestUrl = context.GetRequestUrl();
            if (requestUrl.Equals("/ui", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Response.Redirect(context.Request.Url + "/");
                return Completed;
            }

            var doc = Url.Match(requestUrl).Groups[1].Value;

            var basePath = HttpRuntime.AppDomainAppPath;
            var webUIPath = Path.Combine(basePath, "WebUI");

            if (string.IsNullOrEmpty(doc))
            {
                doc = "studio.html";
            }

            var path = GetPaths(basePath, webUIPath, doc).FirstOrDefault(File.Exists);

            if (path != null)
            {
                context.Response.WriteFile(path);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

            return Completed;
        }

        private IEnumerable<string> GetPaths(string appPath, string webUIPath, string fileName)
        {
            if (fileName.Equals(SilverlightXAP, StringComparison.InvariantCultureIgnoreCase))
            {
                yield return Path.Combine(appPath, "../RavenFS.Studio/bin/debug", fileName);
                yield return Path.Combine(webUIPath, fileName);
                yield return Path.Combine(appPath, fileName);
            }
            else
            {
                yield return Path.Combine(webUIPath, fileName);
            }
        } 
    }
}