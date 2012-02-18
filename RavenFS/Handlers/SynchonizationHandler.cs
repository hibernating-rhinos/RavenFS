using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
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
    [HandlerMetadata("^/synchronize/(.+?)/(.+)$", "GET")]
    public class SynchonizationHandler : AbstractAsyncHandler
    {
        private IDictionary<string, ISignatureRepository> remoteSignatureCaches;

        private static NameValueCollection KnownServers
        {
            get
            {
                return (NameValueCollection)ConfigurationManager.GetSection("knownServers");
            }
        }

        public SynchonizationHandler()
        {
            foreach (var item in KnownServers.AllKeys)
            {
                remoteSignatureCaches[item] = new SimpleSignatureRepository(Path.Combine(Directory.GetCurrentDirectory(), item));
            }
        }

        protected override Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.BufferOutput = false;
            var sourceServerName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;
            var sourceServerUrl = KnownServers[sourceServerName];

            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new Exception("Unknown server identifier " + sourceServerName);
            }

            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[2].Value;
            var sourceRdcAccess = new RemoteRdcAccess(sourceServerUrl);

            var seedRdcAccess = new LocalRdcAccess(Storage, SignatureRepository, SigGenerator);
            var seedSignatureManifest = seedRdcAccess.PrepareSignaturesAsync(fileName).Result;
            var sourceSignatureManifest = sourceRdcAccess.PrepareSignaturesAsync(fileName).Result;

            // download last signature
            // TODO: Recursive signature download
            var sourceSignature = sourceSignatureManifest.Signatures.Last();

            var sourceSignatureInfo = new SignatureInfo(sourceSignature.Name);
            var signatureContent = remoteSignatureCaches[sourceServerName].CreateContent(sourceSignatureInfo.Name);
            sourceRdcAccess.GetSignatureContentAsync(sourceSignature.Name, signatureContent)
                .ContinueWith(_ => signatureContent.Close()).Wait();             
            var seedSignatureInfo = new SignatureInfo(seedSignatureManifest.Signatures.Last().Name);
            var needList = NeedListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);

            ParseNeedList(fileName, fileName, fileName + ".result", needList, sourceRdcAccess);

            return WriteJson(context, new
                                          {
                                              sourceServerUrl,
                                              fileName
                                          });
        }

        private void ParseNeedList(string sourceFileName, string seedFileName, string outputFileName,
            IEnumerable<RdcNeed> needList, IRdcAccess sourceRdcAccess)
        {
            // Currently it copies whole file but it should only replace changed pages
            // TODO: This cast from ulong to long can be dangerous
            // TODO: Improve writting logic by replacing only those pages which was changed
            using (var seedFile = StorageStream.Reading(Storage, seedFileName))
            using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, outputFileName,
                                                                                seedFile.Metadata))
            {
                foreach (var item in needList)
                {
                    switch (item.blockType)
                    {
                        case RdcNeedType.Source:
                            sourceRdcAccess.GetFileContentAsync(sourceFileName, outputFile, (long)item.fileOffset,
                                                                (long)item.blockLength).Wait();
                            break;
                        case RdcNeedType.Seed:
                            // TODO: Some problem with writting to the end
                            new NarrowedStream(seedFile, (long)item.fileOffset, (long)item.fileOffset + (long)item.blockLength - 1).CopyToAsync(
                                outputFile).Wait();
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}