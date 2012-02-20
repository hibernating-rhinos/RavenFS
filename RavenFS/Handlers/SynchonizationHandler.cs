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
using RavenFS.Storage;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Handlers
{
    [HandlerMetadata("^/synchronize/(.+?)/(.+)$", "GET")]
    public class SynchonizationHandler : AbstractAsyncHandler
    {
        private readonly IDictionary<string, ISignatureRepository> _remoteSignatureCaches = new Dictionary<string, ISignatureRepository>();
        private NeedListParser _needListParser = new NeedListParser();

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
                _remoteSignatureCaches[item] = new SimpleSignatureRepository(Path.Combine(Directory.GetCurrentDirectory(), item));
            }
        }

        protected override Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.BufferOutput = false;
            var sourceServerName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;
            var sourceServerUrl = KnownServers[sourceServerName];
            var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
            var remoteRdcManager = new RemoteRdcManager(sourceServerUrl, SignatureRepository, _remoteSignatureCaches[sourceServerName]);

            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new Exception("Unknown server identifier " + sourceServerName);
            }

            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[2].Value;
            var localFileDataInfo = GetLocalFileDataInfo(fileName);

            var seedSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);
            var sourceSignatureManifest = remoteRdcManager.SynchronizeSignatures(localFileDataInfo);

            var seedSignatureInfo = new SignatureInfo(seedSignatureManifest.Signatures.Last().Name);
            var sourceSignatureInfo = new SignatureInfo(sourceSignatureManifest.Signatures.Last().Name);

            // TODO: Copy source Metadata except update time
            // TODO: Return synchronization report
            using (var needListGenerator = new NeedListGenerator(SignatureRepository, _remoteSignatureCaches[sourceServerName]))
            using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                                new NameValueCollection()))
            {
                var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
                _needListParser.Parse(
                    new RemotePartialAccess(sourceServerUrl, fileName),
                    new StoragePartialAccess(Storage, fileName),
                    outputFile, needList);
            }

            return WriteJson(context, new
            {
                sourceServerUrl,
                fileName
            });
        }

        private DataInfo GetLocalFileDataInfo(string fileName)
        {
            FileAndPages fileAndPages = null;
            Storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
            return new DataInfo
                       {
                           CreatedAt = Convert.ToDateTime(fileAndPages.Metadata["Last-Modified"]),
                           Length = fileAndPages.TotalSize ?? 0,
                           Name = fileAndPages.Name
                       };
        }
    }
}