using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/manifest/?$", "GET")]
    public class RdcManifestHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            var filePath = HttpUtility.UrlDecode(context.Request.QueryString["start"]);
            GenerateSignatures(filePath);

            var signatureManifest = new SignatureManifest()
                                        {
                                            
                                        };
            return WriteJson(context, signatureManifest);
        }

        private void GenerateSignatures(string filePath)
        {
            /*
            FileHeader fileHeader = null;
            Storage.Batch(accessor => fileHeader = accessor. .ReadFile(filePath));
            fileHeader.
             * */
        }
    }
}