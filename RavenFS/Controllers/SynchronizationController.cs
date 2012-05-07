using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Notifications;
using RavenFS.Rdc;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using Rdc.Conflictuality;
	using Rdc.Multipart;
	using ConflictDetected = Notifications.ConflictDetected;

	public class SynchronizationController : RavenController
    {
		[AcceptVerbs("POST")]
    	public HttpResponseMessage SynchronizeDestinations(string fileName)
    	{
    		RavenFileSystem.SynchronizationTask.SynchronizeDestinations(fileName);

			return new HttpResponseMessage(HttpStatusCode.NoContent);
    	}

		[AcceptVerbs("POST")]
		public Task<SynchronizationReport> Start(string fileName, string destinationServerUrl)
		{
			return RavenFileSystem.SynchronizationTask.StartSyncingToAsync(fileName, destinationServerUrl);
		}

		[AcceptVerbs("POST")]
		public Task<HttpResponseMessage<SynchronizationReport>> MultipartProceed()
		{
			if (!Request.Content.IsMimeMultipartContent())
			{
				throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
			}

			return InnerMultipartProceed(Request);
		}

		private Task<HttpResponseMessage<SynchronizationReport>> InnerMultipartProceed(HttpRequestMessage request)
		{
			string fileName = Request.Headers.GetValues(SyncingMultipartConstants.FileName).FirstOrDefault();
			string tempFileName = SynchronizationHelper.DownloadingFileName(fileName);

			string sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerUrl).FirstOrDefault();
			Guid lastEtagFromSource = Request.Headers.Value<Guid>("ETag");

			AssertFileIsNotBeingSynced(fileName);

			StartupProceed(fileName);

			FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerUrl);

			StorageStream localFile = null;
			StorageStream synchronizingFile = null;

			bool isConflictResolved = false;

			var sourceMetadata = Request.Headers.FilterHeaders();

			return request.Content.ReadAsMultipartAsync()
				.ContinueWith(multipartReadTask =>
				              	{
				              		var localMetadata = GetLocalMetadata(fileName);

									if (localMetadata != null)
									{
										var conflict = ConflictDetector.Check(localMetadata, sourceMetadata);
										isConflictResolved = ConflictResolver.IsResolved(localMetadata, conflict);

										if (conflict != null && !isConflictResolved)
										{
											ConflictActifactManager.CreateArtifact(fileName, conflict);

											Publisher.Publish(new ConflictDetected
											                  	{
											                  		FileName = fileName,
											                  		ServerUrl = Request.GetServerUrl()
											                  	});

											throw new SynchronizationException(string.Format("File {0} is conflicted", fileName));
										}

										localFile = StorageStream.Reading(Storage, fileName);
									}

									HistoryUpdater.UpdateLastModified(sourceMetadata);

									synchronizingFile = StorageStream.CreatingNewAndWritting(Storage, Search,
																								  tempFileName,
																								  sourceMetadata);

									long sourceBytes = 0;
									long seedBytes = 0;
									long numberOfFileParts = multipartReadTask.Result.Count();

									foreach (var fileChunkPart in multipartReadTask.Result)
									{
										var parameters = fileChunkPart.Headers.ContentDisposition.Parameters.ToDictionary(t => t.Name);

										var needType = parameters[SyncingMultipartConstants.NeedType].Value;
										var from = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeFrom].Value);
										var to = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeTo].Value);

										if (needType == "source")
										{
											sourceBytes += (to - from);
											fileChunkPart.CopyToAsync(synchronizingFile).Wait();
										}
										else if (needType == "seed")
										{
											seedBytes += (to - from);
											localFile.CopyToAsync(synchronizingFile, from, to).Wait();
										}
									}

									return new SynchronizationReport
									{
										FileName = fileName,
										BytesTransfered = sourceBytes,
										BytesCopied = seedBytes,
										NeedListLength = numberOfFileParts
									};
								})
				.ContinueWith(task =>
				{
					if(synchronizingFile != null)
					{
						synchronizingFile.Dispose();
					}

					if(localFile != null)
					{
						localFile.Dispose();
					}

					task.AssertNotFaulted();

					Storage.Batch(
						accessor =>
						{
							accessor.Delete(fileName);
							accessor.RenameFile(tempFileName, fileName);
						});

					if (isConflictResolved)
                    {
						ConflictActifactManager.RemoveArtifact(fileName);
                    } 

					return task.Result;
				})
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

								if (task.Status != TaskStatus.Faulted)
								{
									SaveSynchronizationSourceInformation(sourceServerUrl, lastEtagFromSource, accessor);
								}
							});
						return task;
					})
					.Unwrap()
					.ContinueWith(task =>
					{
						task.AssertNotFaulted();
						return new HttpResponseMessage<SynchronizationReport>(task.Result);
					});
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
		public Task<HttpResponseMessage> ResolveConflict(string fileName, ConflictResolutionStrategy strategy, string sourceServerUrl)
        {
			if (strategy == ConflictResolutionStrategy.CurrentVersion)
            {
                return StrategyAsGetCurrent(fileName, sourceServerUrl)
                    .ContinueWith(
                        task =>
                        {
                            task.AssertNotFaulted();
                            return new HttpResponseMessage();
                        });
            }
            StrategyAsGetRemote(fileName, sourceServerUrl);
            return new CompletedTask<HttpResponseMessage>(new HttpResponseMessage());
        }

        [AcceptVerbs("PATCH")]
        public HttpResponseMessage ApplyConflict(string filename, long remoteVersion, string remoteServerId)
        {
        	var localMetadata = GetLocalMetadata(filename);

        	if (localMetadata == null)
        	{
				throw new HttpResponseException(HttpStatusCode.NotFound);
        	}

        	var conflict = new ConflictItem
        	               	{
        	               		Current = new HistoryItem
        	               		          	{
        	               		          		ServerId = Storage.Id.ToString(),
        	               		          		Version =
        	               		          			long.Parse(localMetadata[SynchronizationConstants.RavenReplicationVersion])
        	               		          	},
        	               		Remote = new HistoryItem
        	               		         	{
        	               		         		ServerId = remoteServerId,
        	               		         		Version = remoteVersion
        	               		         	}
        	               	};

            ConflictActifactManager.CreateArtifact(filename, conflict);

			Publisher.Publish(new ConflictDetected
			{
				FileName = filename,
				ServerUrl = Request.GetServerUrl()
			});

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

		[AcceptVerbs("PATCH")]
		public Task<HttpResponseMessage> ResolveConflictInFavorOfDest(string filename, long remoteVersion, string remoteServerId)
		{
			ApplyConflict(filename, remoteVersion, remoteServerId);

			return ResolveConflict(filename, ConflictResolutionStrategy.RemoteVersion, Request.GetServerUrl());
		}

    	[AcceptVerbs("GET")]
    	public HttpResponseMessage<Guid> LastEtag(string from)
    	{
    		Guid lastEtag = Guid.Empty;
			Storage.Batch(accessor => lastEtag = GetLastEtag(StringUtils.RemoveTrailingSlashAndEncode(from), accessor));
    		return new HttpResponseMessage<Guid>(lastEtag);
    	}

        private Task StrategyAsGetCurrent(string fileName, string sourceServerUrl)
        {
            var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);
			ConflictActifactManager.RemoveArtifact(fileName);
            var localMetadata = GetLocalMetadata(fileName);
            var version = long.Parse(localMetadata[SynchronizationConstants.RavenReplicationVersion]);
        	return sourceRavenFileSystemClient.Synchronization.ResolveConflictInFavorOfDestAsync(fileName, version,
        	                                                                                     Storage.Id.ToString());
        }

        private void StrategyAsGetRemote(string fileName, string sourceServerUrl)
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
                                Strategy = ConflictResolutionStrategy.RemoteVersion,
                                RemoteServerUrl = sourceServerUrl,
                                RemoteServerId = conflictItem.Remote.ServerId,
                                Version = conflictItem.Remote.Version,
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

		private Guid GetLastEtag(string from, StorageActionsAccessor accessor)
		{
			SynchronizationSourceInformation info = null;
			accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationSourcesBasePath + "/" + from, out info);

			return info != null ? info.LastDocumentEtag : Guid.Empty;
		}

		private void SaveSynchronizationSourceInformation(string sourceServerUrl, Guid lastSourceEtag, StorageActionsAccessor accessor)
		{
			var existingLastEtag = GetLastEtag(StringUtils.RemoveTrailingSlashAndEncode(sourceServerUrl), accessor);
			if (string.Compare(existingLastEtag.ToString(), lastSourceEtag.ToString()) > 0)
			{
			    return;
			}

			var synchronizationSourceInfo = new SynchronizationSourceInformation
												{
													LastDocumentEtag = lastSourceEtag,
													ServerInstanceId = Storage.Id
												};

			var key = SynchronizationConstants.RavenReplicationSourcesBasePath + "/" + StringUtils.RemoveTrailingSlashAndEncode(sourceServerUrl);

			accessor.SetConfigurationValue(key, synchronizationSourceInfo);
		}
    }
}
