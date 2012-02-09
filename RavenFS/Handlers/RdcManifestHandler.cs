using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using RavenFS.Infrastructure;
using RavenFS.Storage;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/rdc/manifest/(.+)", "GET")]
    public class RdcManifestHandler : AbstractAsyncHandler
    {
        private readonly FileAccessTool fileAccessTool;

        public RdcManifestHandler()
        {
            fileAccessTool = new FileAccessTool(this);
        }

        protected override Task ProcessRequestAsync(HttpContext context)
        {
            var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

            FileAndPages fileAndPages = null;
            try
            {
                Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = 404;
                return Completed;
            }
            return GenerateSignatures(fileAndPages)
                .ContinueWith(task =>
                    WriteJson(context, task.Result)
                );
        }

        private Task<SignatureManifest> GenerateSignatures(FileAndPages fileAndPages)
        {
            Storage.Batch(accessor => accessor.ReadFile(fileAndPages.Name));

            // TODO: We need to add some cache logic and create Stream own implementation to access FSFiles
            var fileName = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var file = FileAccess.Create(fileName);
            return fileAccessTool.WriteFile(file, fileAndPages.Name, 0, null)
                .ContinueWith(task => file.Close())
                .ContinueWith(
                    task =>
                        {
                            using (var inputFile = FileAccess.OpenRead(fileName))
                            {
                                return SigGenerator.GenerateSignatures(inputFile);
                            }
                        })
                .ContinueWith(
                    task =>
                        {
                            var signatures =
                                from item in task.Result
                                select
                                    new Signature()
                                        {
                                            Length = item.Length,
                                            Name = item.Name
                                        };
                            return
                                new SignatureManifest()
                                    {
                                        FileName = fileAndPages.Name,
                                        FileLength = fileAndPages.TotalSize ?? 0,
                                        Signatures = signatures.ToList()
                                    };

                        });
        }
    }
}