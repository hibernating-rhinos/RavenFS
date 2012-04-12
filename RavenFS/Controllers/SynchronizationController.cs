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
        public Task<HttpResponseMessage<SynchronizationReport>> Get(string fileName, string sourceServerUrl)
        {
            //return new CompletedTask<HttpResponseMessage<SynchronizationReport>>(new HttpResponseMessage<SynchronizationReport>(HttpStatusCode.Conflict));
            if (String.IsNullOrEmpty(sourceServerUrl))
            {
                throw new HttpResponseException("Unknown server identifier " + sourceServerUrl, HttpStatusCode.ServiceUnavailable);
            }

            if (FileIsBeingSynced(fileName))
            {
                throw new HttpResponseException(string.Format("File {0} is being synced", fileName), HttpStatusCode.ServiceUnavailable);
            }

            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);


            LockFileByCreatingSyncConfiguration(fileName, sourceServerUrl);
            // TODO: Not sure if unlocking is run if FatalError method has been called. (CompletedTask)

            return sourceRavenFileSystemClient.GetMetadataForAsync(fileName)
                .ContinueWith(
                    getMetadataForAsyncTask =>
                    {
                        var remoteMetadata = getMetadataForAsyncTask.Result;
                        if (remoteMetadata.AllKeys.Contains(ReplicationConstants.RavenReplicationConflict))
                        {
                            throw new HttpResponseException(string.Format("File {0} on THEIR side is conflicted", fileName), HttpStatusCode.ServiceUnavailable);
                        }

                        var localFileDataInfo = GetLocalFileDataInfo(fileName);

                        if (localFileDataInfo == null)
                        {
                            // if file doesn't exist locally - download it at all
                            return Download(sourceRavenFileSystemClient, fileName, remoteMetadata)
                                .ContinueWith(
                                    synchronizationTask =>
                                    new HttpResponseMessage<SynchronizationReport>(synchronizationTask.Result));
                        }

                        var localMetadata = GetLocalMetadata(fileName);

                        var conflict = CheckConflict(localMetadata, remoteMetadata);
                        var isConflictResolved = IsConflictResolved(localMetadata, conflict);
                        if (conflict != null && !isConflictResolved)
                        {
                            Storage.Batch(
                                accessor =>
                                {
                                    accessor.SetConfigurationValue(
                                        ReplicationHelper.ConflictConfigNameForFile(fileName), conflict);
                                    localMetadata[ReplicationConstants.RavenReplicationConflict] =
                                        true.ToString();
                                    accessor.UpdateFileMetadata(fileName, localMetadata);
                                });
                        	
                        	Publisher.Publish(new ConflictDetected
                        	                  	{
                        	                  		FileName = fileName,
                        	                  		ServerUrl = Request.GetServerUrl()
							                  	});

                            throw new HttpResponseException(string.Format("File {0} is conflicted", fileName), HttpStatusCode.Conflict);
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
                                    if (isConflictResolved)
                                    {
                                        RemoveConflictArtifacts(localMetadata, fileName);
                                    }

                                    remoteSignatureCache.Dispose();
                                    var synchronizationReport = synchronizationTask.Result;

                                    return
                                        new HttpResponseMessage<SynchronizationReport>(synchronizationReport);
                                });
                    })
                .Unwrap()
                .ContinueWith(
                    task =>
                    {
                        UnlockFileByDeletingSyncConfiguration(fileName);
                        return task.Result;
                    });
        }

        private void RemoveConflictArtifacts(NameValueCollection localMetadata, string fileName)
        {
            Storage.Batch(
                accessor =>
                {
                    accessor.DeleteConfig(ReplicationHelper.ConflictConfigNameForFile(fileName));
                });
        }

        private bool IsConflictResolved(NameValueCollection localMetadata, ConflictItem conflict)
        {
            var conflictResolutionString = localMetadata[ReplicationConstants.RavenReplicationConflictResolution];
            if (String.IsNullOrEmpty(conflictResolutionString))
            {
                return false;
            }
            var conflictResolution = new TypeHidingJsonSerializer().Parse<ConflictResolution>(conflictResolutionString);
            return conflictResolution.Strategy == ConflictResolutionStrategy.GetTheirs
                && conflictResolution.TheirServerId == conflict.Theirs.ServerId;
        }

        [AcceptVerbs("PATCH")]
        public HttpResponseMessage Patch(string fileName, string strategy, string sourceServerUrl)
        {
            var selectedStrategy = ConflictResolutionStrategy.GetTheirs;
            Enum.TryParse<ConflictResolutionStrategy>(strategy, true, out selectedStrategy);
            InnerResolveConflict(fileName, sourceServerUrl, selectedStrategy);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        private void InnerResolveConflict(string fileName, string sourceServerUrl, ConflictResolutionStrategy strategy)
        {
            if (strategy == ConflictResolutionStrategy.GetOurs)
            {
                throw new NotImplementedException("Not implemented yet");
                // TODO Set on remote GetTheirs strategy and run synchronization with our url
            }
            else if (strategy == ConflictResolutionStrategy.GetTheirs)
            {
                Storage.Batch(
                    accessor =>
                    {
                        var localMetadata = accessor.GetFile(fileName, 0, 0).Metadata;
                        var conflictConfigName = ReplicationHelper.ConflictConfigNameForFile(fileName);
                        var conflictItem = accessor.GetConfigurationValue<ConflictItem>(conflictConfigName);

                        var conflictResolution =
                            new ConflictResolution
                                {
                                    Strategy = ConflictResolutionStrategy.GetTheirs,
                                    TheirServerUrl = sourceServerUrl,
                                    TheirServerId = conflictItem.Theirs.ServerId,
                                    Version = conflictItem.Theirs.Version,
                                };
                        localMetadata[ReplicationConstants.RavenReplicationConflictResolution] =
                            new TypeHidingJsonSerializer().Stringify(conflictResolution);
                        accessor.UpdateFileMetadata(fileName, localMetadata);
                    });
            }
            else
            {
                throw new NotSupportedException(String.Format("Strategy {0} is not supported", strategy));
            }
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
            var remoteVersion = long.Parse(remoteMetadata[ReplicationConstants.RavenReplicationVersion]);
            var remoteServerId = remoteMetadata[ReplicationConstants.RavenReplicationSource];
            var localVersion = long.Parse(localMetadata[ReplicationConstants.RavenReplicationVersion]);
            var localServerId = localMetadata[ReplicationConstants.RavenReplicationSource];
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
            var tempFileName = fileName + ".result";
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
            var tempFileName = fileName + ".result";
            var storageStream = StorageStream.CreatingNewAndWritting(Storage, Search, tempFileName,
                                                                     sourceMetadata.FilterHeaders());
            return sourceRavenFileSystemClient.DownloadAsync(fileName, storageStream)
                .ContinueWith(
                    _ =>
                    {
                        storageStream.Dispose();
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

        private void LockFileByCreatingSyncConfiguration(string fileName, string sourceServerUrl)
        {
            Storage.Batch(accessor =>
                            {
                                var syncOperationDetails = new NameValueCollection
			              		                           	{
			              		                           		{ ReplicationConstants.RavenReplicationSource, sourceServerUrl }
			              		                           	};

                                accessor.SetConfig(ReplicationHelper.SyncConfigNameForFile(fileName), syncOperationDetails);
                            });
        }



        private void UnlockFileByDeletingSyncConfiguration(string fileName)
        {
            Storage.Batch(accessor => accessor.DeleteConfig(ReplicationHelper.SyncConfigNameForFile(fileName)));
        }
    }
}