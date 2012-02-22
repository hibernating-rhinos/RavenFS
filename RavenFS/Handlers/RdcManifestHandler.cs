using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using RavenFS.Infrastructure;
using RavenFS.Rdc;
using RavenFS.Storage;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/manifest/(.+)", "GET")]
    public class RdcManifestHandler : AbstractAsyncHandler
    {
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

            try
            {
                Storage.Batch(accessor => accessor.GetFile(filename, 0, 0));
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = 404;
                return Completed;
            }

            return GenerateSignatures(filename)
                .ContinueWith(task =>
                    WriteJson(context, task.Result)
                );
        }

        private Task<SignatureManifest> GenerateSignatures(string fileName)
        {
            var rdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
            var result =
                new Task<SignatureManifest>(() => rdcManager.GetSignatureManifest(new DataInfo() {Name = fileName}));            
            result.Start();
            return result;
        }
    }
}