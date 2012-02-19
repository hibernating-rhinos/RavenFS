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
        private IDictionary<string, ISignatureRepository> remoteSignatureCaches = new Dictionary<string, ISignatureRepository>();
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

            var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
            var seedSignatureManifest = localRdcManager.GetSignatureManifest(GetLocalFileDataInfo(fileName));
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


            // TODO: Copy source Metadata except update time
            using(var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                                new NameValueCollection()))
            {
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