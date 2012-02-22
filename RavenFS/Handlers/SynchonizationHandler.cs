using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Raven.Abstractions.Extensions;
using RavenFS.Client;
using RavenFS.Infrastructure;
using RavenFS.Rdc;
using RavenFS.Storage;
using RavenFS.Util;
using Rdc.Wrapper;
using RavenFS.Extensions;

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
            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);
            var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
            var remoteRdcManager = new RemoteRdcManager(sourceRavenFileSystemClient, SignatureRepository, _remoteSignatureCaches[sourceServerName]);            

            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new Exception("Unknown server identifier " + sourceServerName);
            }

            var fileName = Url.Match(context.Request.CurrentExecutionFilePath).Groups[2].Value;
            var sourceMetadataAsync = sourceRavenFileSystemClient.GetMetadataForAsync(fileName)
                .ContinueWith(task => task.Result.UpdateLastModified());
            var localFileDataInfo = GetLocalFileDataInfo(fileName);

            var seedSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);
            var sourceSignatureManifest = remoteRdcManager.SynchronizeSignatures(localFileDataInfo);

            var report = new SynchronizationReport {FileName = fileName};
            if (sourceSignatureManifest.Signatures.Count > 0)
            {
                var seedSignatureInfo = new SignatureInfo(seedSignatureManifest.Signatures.Last().Name);
                var sourceSignatureInfo = new SignatureInfo(sourceSignatureManifest.Signatures.Last().Name);
                
                using (
                    var needListGenerator = new NeedListGenerator(SignatureRepository,
                                                                  _remoteSignatureCaches[sourceServerName]))
                using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                             sourceMetadataAsync.Result))
                {
                    var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
                    _needListParser.Parse(
                        new RemotePartialAccess(sourceServerUrl, fileName),
                        new StoragePartialAccess(Storage, fileName),
                        outputFile, needList);
                    report.BytesTransfered =
                        needList.Sum(item => item.blockType == RdcNeedType.Source ? (long)item.blockLength : 0L);
                    report.BytesCopied =
                        needList.Sum(item => item.blockType == RdcNeedType.Seed ? (long)item.blockLength : 0L);
                }
            } 
            else
            {
                using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                             sourceMetadataAsync.Result))
                {
                    sourceRavenFileSystemClient.DownloadAsync(fileName, outputFile).Wait();
                }
                report.BytesCopied = StorageStream.Reading(Storage, fileName + ".result").Length;
            }

            return WriteJson(context, report);
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