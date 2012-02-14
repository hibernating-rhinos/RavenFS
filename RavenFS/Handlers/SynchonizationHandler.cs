using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Rdc;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/synchronize/(.+)/(.+)", "GET")]
    public class SynchonizationHandler : AbstractAsyncHandler
    {        
        protected override Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.BufferOutput = false;            
            var sourceServer = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;
            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[2].Value;
            var sourceRdcAccess = new RemoteRdcAccess(sourceServer);

            var seedRdcAccess = new LocalRdcAccess(new FileAccessTool(this), Storage, FileAccess, SigGenerator);
            var seedSignatureManifest = seedRdcAccess.GetRdcManifestAsync(fileName).Result;            
            var sourceSignatureManifest = sourceRdcAccess.GetRdcManifestAsync(fileName).Result;
            
            // download last signature
            // TODO: Recursive signature download
            var sourceSignature = sourceSignatureManifest.Signatures.Last();
            
            var sourceSignatureInfo = new SignatureInfo(FileAccess, sourceSignature.Name);
            var signatureContent = sourceSignatureInfo.OpenWrite();
            sourceRdcAccess.GetSignatureContentAsync(sourceSignature.Name, signatureContent)
                .ContinueWith(_ => signatureContent.Close());
            var seedSignatureInfo = new SignatureInfo(FileAccess, seedSignatureManifest.Signatures.Last().Name);
            var needList = NeedListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);            

            return null;
        }

        private void ParseNeedList(string sourceFileName, string seedFileName, string outputPath, IEnumerable<RdcNeed> needList, IRdcAccess sourceRdcAccess)
        {            
            // Currently it copies whole file but it should only replace changed pages
            // TODO: This cast from ulong to long can be dangerous
            // TODO: Improve writting logic by replacing only those pages which was changed
            using (Stream seedFile = new StorageStream(Storage, seedFileName), outputFile = new StorageStream(Storage, outputPath + ".result"))
            {
                foreach (var item in needList)
                {
                    switch (item.blockType)
                    {
                        case RdcNeedType.Source:                            
                            sourceRdcAccess.GetFileContentAsync(sourceFileName, outputFile, (long)item.fileOffset, (long)item.blockLength).Wait();
                            break;
                        case RdcNeedType.Seed:                            
                            new NarrowedStream(seedFile, (long)item.fileOffset, (long)item.blockLength).CopyToAsync(outputFile).Wait();
                            break;
                        default:
                            break;
                    }
                }
            }            
        }
    }
}