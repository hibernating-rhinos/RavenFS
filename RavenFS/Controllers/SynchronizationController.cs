using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using RavenFS.Storage;
using RavenFS.Util;
using RavenFS.Extensions;

namespace RavenFS.Controllers
{
    public class SynchronizationController : RavenController
    {
        public HttpResponseMessage<SynchronizationReport> Get(string fileName, string sourceServerUrl)
        {

            var remoteSignatureCache = new SimpleSignatureRepository(GetTemporaryDirectory());

            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);
            var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
            var remoteRdcManager = new RemoteRdcManager(sourceRavenFileSystemClient, SignatureRepository, remoteSignatureCache);

            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new Exception("Unknown server identifier " + sourceServerUrl);
            }
            var sourceMetadataAsync = sourceRavenFileSystemClient.GetMetadataForAsync(fileName)
                .ContinueWith(task => task.Result.UpdateLastModified());
            var localFileDataInfo = GetLocalFileDataInfo(fileName);

            var seedSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);
            var sourceSignatureManifest = remoteRdcManager.SynchronizeSignatures(localFileDataInfo);

            SynchronizationReport report = null;
            if (sourceSignatureManifest.Signatures.Count > 0)
            {
                report = Synchronize(remoteSignatureCache, sourceServerUrl, fileName, sourceSignatureManifest, seedSignatureManifest, sourceMetadataAsync);
            }
            else
            {
                report = Download(sourceRavenFileSystemClient, fileName, sourceMetadataAsync);
            }

            return new HttpResponseMessage<SynchronizationReport>(report);
        }

        private static string GetTemporaryDirectory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private SynchronizationReport Synchronize(ISignatureRepository remoteSignatureRepository, string sourceServerUrl, string fileName, SignatureManifest sourceSignatureManifest, SignatureManifest seedSignatureManifest, Task<NameValueCollection> sourceMetadata)
        {
            var result = new SynchronizationReport { FileName = fileName };
            var seedSignatureInfo = new SignatureInfo(seedSignatureManifest.Signatures.Last().Name);
            var sourceSignatureInfo = new SignatureInfo(sourceSignatureManifest.Signatures.Last().Name);

            using (
                var needListGenerator = new NeedListGenerator(SignatureRepository, remoteSignatureRepository))
            using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                         sourceMetadata.Result.FilterHeaders()))
            {
                var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
                NeedListParser.Parse(
                    new RemotePartialAccess(sourceServerUrl, fileName),
                    new StoragePartialAccess(Storage, fileName),
                    outputFile, needList);
                result.BytesTransfered =
                    needList.Sum(item => item.BlockType == RdcNeedType.Source ? (long)item.BlockLength : 0L);
                result.BytesCopied =
                    needList.Sum(item => item.BlockType == RdcNeedType.Seed ? (long)item.BlockLength : 0L);
                result.NeedListLength = needList.Count;
            }
            return result;
        }

        private SynchronizationReport Download(RavenFileSystemClient sourceRavenFileSystemClient, string fileName, Task<NameValueCollection> sourceMetadataAsync)
        {
            var result = new SynchronizationReport { FileName = fileName };
            using (var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search, fileName + ".result",
                                                                         sourceMetadataAsync.Result.FilterHeaders()))
            {
                sourceRavenFileSystemClient.DownloadAsync(fileName, outputFile).Wait();
            }
            result.BytesCopied = StorageStream.Reading(Storage, fileName + ".result").Length;
            return result;
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