using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;
using RavenFS.Client;
using RavenFS.Notifications;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using RavenFS.Storage;
using RavenFS.Util;
using RavenFS.Extensions;
using RavenFS.Infrastructure;

namespace RavenFS.Controllers
{
    using ConflictDetected = Notifications.ConflictDetected;

    public class SynchronizationController : RavenController
    {
        [AcceptVerbs("POST")]
        public HttpResponseMessage Proceed(string fileName, string sourceServerUrl)
        {
            StartupProceed(fileName);

            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new HttpResponseException("Unknown server identifier " + sourceServerUrl, HttpStatusCode.BadRequest);
            }

            InnerProceed(fileName, sourceServerUrl);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        private void StartupProceed(string fileName)
        {
            Storage.Batch(
                accessor =>
                    {
                        // remove previous SyncResult
                        var name = SynchronizationHelper.SyncResultNameForFile(fileName);
                        accessor.DeleteConfig(name);

                        // remove previous .downloading file
                        if (accessor.ConfigExists(SynchronizationHelper.SyncNameForFile(fileName)) == false)
                        {
                            Search.Delete(name);
                            accessor.Delete(SynchronizationHelper.DownloadingFileName(fileName));
                        }
                    });
        }

        private Task InnerProceed(string fileName, string sourceServerUrl)
        {
            AssertFileIsNotBeingSynced(fileName);

            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);

            FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerUrl);

            var result = sourceRavenFileSystemClient.GetMetadataForAsync(fileName)
                .ContinueWith(
                    getMetadataForAsyncTask =>
                    {
                        var remoteMetadata = getMetadataForAsyncTask.Result;
                        if (remoteMetadata.AllKeys.Contains(SynchronizationConstants.RavenReplicationConflict))
                        {
                            throw new SynchronizationException(
                                string.Format("File {0} on THEIR side is conflicted", fileName));
                        }

                        var localFileDataInfo = GetLocalFileDataInfo(fileName);

                        if (localFileDataInfo == null)
                        {
                            // if file doesn't exist locally - download all of it
                            return Download(sourceRavenFileSystemClient, fileName, remoteMetadata);
                        }

                        var localMetadata = GetLocalMetadata(fileName);

                        var conflict = CheckConflict(localMetadata, remoteMetadata);
                        var isConflictResolved = IsConflictResolved(localMetadata, conflict);
                        if (conflict != null && !isConflictResolved)
                        {
                            CreateConflictArtifacts(fileName, conflict);

                            Publisher.Publish(new ConflictDetected
                                                  {
                                                      FileName = fileName,
                                                      ServerUrl = Request.GetServerUrl()
                                                  });


                            throw new SynchronizationException(string.Format("File {0} is conflicted", fileName));
                        }

                        var remoteSignatureCache = new VolatileSignatureRepository(TempDirectoryTools.Create());
                        var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
                        var remoteRdcManager = new RemoteRdcManager(sourceRavenFileSystemClient, SignatureRepository,
                                                                    remoteSignatureCache);

                        var seedSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);

                        return remoteRdcManager.SynchronizeSignaturesAsync(localFileDataInfo)
                            .ContinueWith(
                                task =>
                                {
                                    var sourceSignatureManifest = task.Result;

                                    if (sourceSignatureManifest.Signatures.Count > 0)
                                        return Synchronize(remoteSignatureCache, sourceServerUrl,
                                                           fileName,
                                                           sourceSignatureManifest,
                                                           seedSignatureManifest,
                                                           remoteMetadata);
                                    return Download(sourceRavenFileSystemClient, fileName,
                                                    remoteMetadata);
                                })
                            .Unwrap()
                            .ContinueWith(
                                synchronizationTask =>
                                {
                                    remoteSignatureCache.Dispose();

                                    if (isConflictResolved)
                                    {
                                        RemoveConflictArtifacts(fileName);
                                    }

                                    return synchronizationTask.Result;
                                });
                    })
                .Unwrap()
                .ContinueWith(
                    task =>
                    {
                        FileLockManager.UnlockByDeletingSyncConfiguration(fileName);
                        return task.Result;
                    })
                .ContinueWith(
                    task =>
                    {
                        SynchronizationReport report;
                        if (task.Status == TaskStatus.Faulted)
                        {
                            report =
                                new SynchronizationReport
                                    {
                                        Exception = task.Exception.ExtractSingleInnerException()
                                    };
                        }
                        else
                        {
                            report = task.Result;
                        }
                        Storage.Batch(
                            accessor => 
                            {
                                var name = SynchronizationHelper.SyncResultNameForFile(fileName);
                                accessor.SetConfigurationValue(name, report);
                            });
                    });
            return result;
        }

        [AcceptVerbs("GET")]
        public HttpResponseMessage<SynchronizationReport> Status(string fileName)
        {
            SynchronizationReport preResult = null;
            Storage.Batch(
                accessor =>
                    {
                        var name = SynchronizationHelper.SyncResultNameForFile(fileName);
                        accessor.TryGetConfigurationValue(name, out preResult);
                    });
            return new HttpResponseMessage<SynchronizationReport>(preResult);
        }

        [AcceptVerbs("GET")]
        public HttpResponseMessage<IEnumerable<SynchronizationReport>> Finished(int page, int pageSize)
        {
            IList<SynchronizationReport> configObjects = null;
            Storage.Batch(
                accessor =>
                    {
                        var configKeys =
                            from item in accessor.GetConfigNames()
                            where SynchronizationHelper.IsSyncResultName(item)
                            select item;
                        configObjects =
                            (from item in configKeys.Skip(pageSize*page).Take(pageSize)
                            select accessor.GetConfigurationValue<SynchronizationReport>(item)).ToList();
                    });
            return new HttpResponseMessage<IEnumerable<SynchronizationReport>>(configObjects);
        }

        [AcceptVerbs("GET")]
        public HttpResponseMessage<IEnumerable<SynchronizationDetails>> Working(int page, int pageSize)
        {
            IList<SynchronizationDetails> configObjects = null;
            Storage.Batch(
                accessor =>
                {
                    var configKeys =
                        from item in accessor.GetConfigNames()
                        where SynchronizationHelper.IsSyncName(item)
                        select item;
                    configObjects =
                        (from item in configKeys.Skip(pageSize * page).Take(pageSize)
                         select accessor.GetConfigurationValue<SynchronizationDetails>(item)).ToList();
                });
            return new HttpResponseMessage<IEnumerable<SynchronizationDetails>>(configObjects);
        }

        [AcceptVerbs("PATCH")]
        public Task<HttpResponseMessage> ResolveConflict(string fileName, string strategy, string sourceServerUrl)
        {
            var selectedStrategy = ConflictResolutionStrategy.Theirs;
            Enum.TryParse(strategy, true, out selectedStrategy);

            if (selectedStrategy == ConflictResolutionStrategy.Ours)
            {
                return StrategyAsGetOurs(fileName, sourceServerUrl)
                    .ContinueWith(
                        task =>
                        {
                            task.Wait();
                            return new HttpResponseMessage();
                        });
            }
            StrategyAsGetTheirs(fileName, sourceServerUrl);
            var result = new Task<HttpResponseMessage>(() => new HttpResponseMessage());
            result.Start();
            return result;
        }

        [AcceptVerbs("PATCH")]
        public HttpResponseMessage ApplyConflict(string filename, long theirVersion, string theirServerId)
        {
            var conflict = new ConflictItem
                               {
                                   Ours = new HistoryItem
                                              {
                                                  ServerId = Storage.Id.ToString(),
                                                  Version =
                                                      long.Parse(
                                                          GetLocalMetadata(filename)[
                                                              SynchronizationConstants.RavenReplicationVersion])
                                              },
                                   Theirs = new HistoryItem
                                                {
                                                    ServerId = theirServerId,
                                                    Version = theirVersion
                                                }
                               };
            CreateConflictArtifacts(filename, conflict);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        private void CreateConflictArtifacts(string fileName, ConflictItem conflict)
        {
            Storage.Batch(
                accessor =>
                {
                    var metadata = accessor.GetFile(fileName, 0, 0).Metadata;
                    accessor.SetConfigurationValue(
                        SynchronizationHelper.ConflictConfigNameForFile(fileName), conflict);
                    metadata[SynchronizationConstants.RavenReplicationConflict] = "True";
                    accessor.UpdateFileMetadata(fileName, metadata);
                });
        }

        private void RemoveConflictArtifacts(string fileName)
        {
            Storage.Batch(
                accessor =>
                {
                    accessor.DeleteConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName));
                    var metadata = accessor.GetFile(fileName, 0, 0).Metadata;
                    metadata.Remove(SynchronizationConstants.RavenReplicationConflict);
                    metadata.Remove(SynchronizationConstants.RavenReplicationConflictResolution);
                    accessor.UpdateFileMetadata(fileName, metadata);
                });
        }

        private bool IsConflictResolved(NameValueCollection localMetadata, ConflictItem conflict)
        {
            var conflictResolutionString = localMetadata[SynchronizationConstants.RavenReplicationConflictResolution];
            if (String.IsNullOrEmpty(conflictResolutionString))
            {
                return false;
            }
            var conflictResolution = new TypeHidingJsonSerializer().Parse<ConflictResolution>(conflictResolutionString);
            return conflictResolution.Strategy == ConflictResolutionStrategy.Theirs
                && conflictResolution.TheirServerId == conflict.Theirs.ServerId;
        }

        private Task StrategyAsGetOurs(string fileName, string sourceServerUrl)
        {
            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);
            RemoveConflictArtifacts(fileName);
            var localMetadata = GetLocalMetadata(fileName);
            var version = long.Parse(localMetadata[SynchronizationConstants.RavenReplicationVersion]);
            return
                sourceRavenFileSystemClient.Synchronization.ApplyConflictAsync(fileName, version, Storage.Id.ToString())
                    .ContinueWith(
                        task =>
                        {
                            task.Wait(); // throw exception
                            return
                                sourceRavenFileSystemClient.Synchronization.ResolveConflictAsync(
                                    Request.GetServerUrl(), fileName,
                                    ConflictResolutionStrategy.Theirs);
                        })
                    .Unwrap();
        }

        private void StrategyAsGetTheirs(string fileName, string sourceServerUrl)
        {
            Storage.Batch(
                accessor =>
                {
                    var localMetadata = accessor.GetFile(fileName, 0, 0).Metadata;
                    var conflictConfigName = SynchronizationHelper.ConflictConfigNameForFile(fileName);
                    var conflictItem = accessor.GetConfigurationValue<ConflictItem>(conflictConfigName);

                    var conflictResolution =
                        new ConflictResolution
                            {
                                Strategy = ConflictResolutionStrategy.Theirs,
                                TheirServerUrl = sourceServerUrl,
                                TheirServerId = conflictItem.Theirs.ServerId,
                                Version = conflictItem.Theirs.Version,
                            };
                    localMetadata[SynchronizationConstants.RavenReplicationConflictResolution] =
                        new TypeHidingJsonSerializer().Stringify(conflictResolution);
                    accessor.UpdateFileMetadata(fileName, localMetadata);
                });
        }

        private NameValueCollection GetLocalMetadata(string fileName)
        {
            NameValueCollection result = null;
            try
            {
                Storage.Batch(
                    accessor =>
                    {
                        result = accessor.GetFile(fileName, 0, 0).Metadata;
                    });
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            return result;
        }

        private ConflictItem CheckConflict(NameValueCollection localMetadata, NameValueCollection remoteMetadata)
        {
            var remoteHistory = HistoryUpdater.DeserializeHistory(remoteMetadata);
            var remoteVersion = long.Parse(remoteMetadata[SynchronizationConstants.RavenReplicationVersion]);
            var remoteServerId = remoteMetadata[SynchronizationConstants.RavenReplicationSource];
            var localVersion = long.Parse(localMetadata[SynchronizationConstants.RavenReplicationVersion]);
            var localServerId = localMetadata[SynchronizationConstants.RavenReplicationSource];
            // if there are the same files or local is direct child there are no conflicts
            if ((remoteServerId == localServerId && remoteVersion == localVersion)
                || remoteHistory.Any(item => item.ServerId == localServerId && item.Version == localVersion))
            {
                return null;
            }
            return
                new ConflictItem
                    {
                        Ours = new HistoryItem { ServerId = localServerId, Version = localVersion },
                        Theirs = new HistoryItem { ServerId = remoteServerId, Version = remoteVersion }
                    };
        }

        private Task<SynchronizationReport> Synchronize(ISignatureRepository remoteSignatureRepository, string sourceServerUrl, string fileName, SignatureManifest sourceSignatureManifest, SignatureManifest seedSignatureManifest, NameValueCollection sourceMetadata)
        {
            var seedSignatureInfo = SignatureInfo.Parse(seedSignatureManifest.Signatures.Last().Name);
            var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
            var needListGenerator = new NeedListGenerator(SignatureRepository, remoteSignatureRepository);
            var tempFileName = SynchronizationHelper.DownloadingFileName(fileName);
            var outputFile = StorageStream.CreatingNewAndWritting(Storage, Search,
                                                                  tempFileName,
                                                                  sourceMetadata.FilterHeaders());

            var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);

            return NeedListParser.ParseAsync(
                new RemotePartialAccess(sourceServerUrl, fileName),
                new StoragePartialAccess(Storage, fileName),
                outputFile, needList).ContinueWith(
                    _ =>
                    {
                        outputFile.Dispose();
                        needListGenerator.Dispose();
                        _.AssertNotFaulted();
                        Storage.Batch(
                            accessor =>
                            {
                                accessor.Delete(fileName);
                                accessor.RenameFile(tempFileName, fileName);
                            });
                        return new SynchronizationReport
                        {
                            FileName = fileName,
                            BytesTransfered = needList.Sum(
                                item =>
                                item.BlockType == RdcNeedType.Source
                                    ? (long)item.BlockLength
                                    : 0L),
                            BytesCopied = needList.Sum(
                                item =>
                                item.BlockType == RdcNeedType.Seed
                                    ? (long)item.BlockLength
                                    : 0L),
                            NeedListLength = needList.Count
                        };
                    });

        }

        private Task<SynchronizationReport> Download(RavenFileSystemClient sourceRavenFileSystemClient, string fileName, NameValueCollection sourceMetadata)
        {
            var tempFileName = SynchronizationHelper.DownloadingFileName(fileName);
            var storageStream = StorageStream.CreatingNewAndWritting(Storage, Search, tempFileName,
                                                                     sourceMetadata.FilterHeaders());
            return sourceRavenFileSystemClient.DownloadAsync(fileName, storageStream)
                .ContinueWith(
                    _ =>
                    {
                        storageStream.Dispose();
                        _.AssertNotFaulted();
                        Storage.Batch(
                            accessor =>
                            {
                                accessor.Delete(fileName);
                                accessor.RenameFile(tempFileName, fileName);
                            });
                        return new SynchronizationReport
                                   {
                                       FileName = fileName,
                                       BytesCopied = StorageStream.Reading(Storage, fileName).Length
                                   };
                    });
        }


        private DataInfo GetLocalFileDataInfo(string fileName)
        {
            FileAndPages fileAndPages = null;
            try
            {
                Storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            return new DataInfo
            {
                CreatedAt = Convert.ToDateTime(fileAndPages.Metadata["Last-Modified"]),
                Length = fileAndPages.TotalSize ?? 0,
                Name = fileAndPages.Name
            };
        }
    }
}
