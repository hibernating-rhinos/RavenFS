namespace RavenFS.Controllers
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Web.Http;
	using Newtonsoft.Json;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Infrastructure;
	using RavenFS.Notifications;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Synchronization;
	using Synchronization.Conflictuality;
	using Synchronization.Multipart;

	public class SynchronizationController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private static readonly ConcurrentDictionary<string, ReaderWriterLockSlim> synchronizationFinishLocks = new ConcurrentDictionary<string, ReaderWriterLockSlim>();

		[AcceptVerbs("POST")]
		public Task<DestinationSyncResult[]> ToDestinations(bool forceSyncingAll)
		{
			return SynchronizationTask.SynchronizeDestinationsAsync(forceSyncingContinuation: forceSyncingAll);
		}

		[AcceptVerbs("POST")]
		public Task<SynchronizationReport> Start(string fileName, string destinationServerUrl)
		{
			log.Debug("Starting to synchronize a file '{0}' to {1}", fileName, destinationServerUrl);

			return SynchronizationTask.SynchronizeFileToAsync(fileName, destinationServerUrl);
		}

		[AcceptVerbs("POST")]
		public async Task<HttpResponseMessage> MultipartProceed()
		{
			if (!Request.Content.IsMimeMultipartContent())
			{
				throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
			}

			var fileName = Request.Headers.GetValues(SyncingMultipartConstants.FileName).FirstOrDefault();
			var tempFileName = RavenFileNameHelper.DownloadingFileName(fileName);

			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault();
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			var report = new SynchronizationReport(fileName, sourceFileETag, SynchronizationType.ContentUpdate);

			log.Debug("Starting to process multipart synchronization request of a file '{0}' with ETag {1} from {2}", fileName,
						sourceFileETag, sourceServerId);

			StorageStream localFile = null;
			var isNewFile = false;
			var isConflictResolved = false;

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
				});

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Start);
				
				Storage.Batch(accessor => StartupProceed(fileName, accessor));

				var sourceMetadata = Request.Headers.FilterHeaders();

				var localMetadata = GetLocalMetadata(fileName);

				if (localMetadata != null)
				{
					AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, sourceServerUrl, out isConflictResolved);
					localFile = StorageStream.Reading(Storage, fileName);
				}
				else
				{
					isNewFile = true;
				}

				Historian.UpdateLastModified(sourceMetadata);

				var synchronizingFile = SynchronizingFileStream.CreatingOrOpeningAndWritting(Storage, Search, StorageOperationsTask, tempFileName, sourceMetadata);
				
				var provider = new MultipartSyncStreamProvider(synchronizingFile, localFile);

				log.Debug("Starting to process multipart content of a file '{0}'", fileName);

				await Request.Content.ReadAsMultipartAsync(provider);

				log.Debug("Multipart content of a file '{0}' was processed", fileName);

				report.BytesCopied = provider.BytesCopied;
				report.BytesTransfered = provider.BytesTransfered;
				report.NeedListLength = provider.NumberOfFileParts;

				synchronizingFile.PreventUploadComplete = false;
				synchronizingFile.Dispose();
				sourceMetadata["Content-MD5"] = synchronizingFile.FileHash;

				Storage.Batch(accesor => accesor.UpdateFileMetadata(tempFileName, sourceMetadata));

				Storage.Batch(accessor =>
				{
					StorageOperationsTask.IndicateFileToDelete(fileName);
					accessor.RenameFile(tempFileName, fileName);

					Search.Delete(tempFileName);
					Search.Index(fileName, sourceMetadata);
				});

				if (isNewFile)
				{
					log.Debug("Temporary downloading file '{0}' was renamed to '{1}'. Indexes was updated.", tempFileName, fileName);
				}
				else
				{
					log.Debug("Old file '{0}' was deleted. Indexes was updated.", fileName);
				}

				if (isConflictResolved)
				{
					ConflictArtifactManager.Delete(fileName);
				}
			}
			catch (Exception ex)
			{
				report.Exception = ex;
			}
			finally
			{
				if (localFile != null)
				{
					localFile.Dispose();
				}
			}

			if (report.Exception == null)
			{
				log.Debug(
					"File '{0}' was synchronized successfully from {1}. {2} bytes were transfered and {3} bytes copied. Need list length was {4}",
					fileName, sourceServerId, report.BytesTransfered, report.BytesCopied, report.NeedListLength);
			}
			else
			{
				log.WarnException(
					string.Format("Error has occured during synchronization of a file '{0}' from {1}", fileName,
								  sourceServerId), report.Exception);
			}

			FinishSynchronization(fileName, report, sourceServerId, sourceFileETag);

			PublishFileNotification(fileName, isNewFile ? FileChangeAction.Add : FileChangeAction.Update);
			PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Finish);

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		private void FinishSynchronization(string fileName, SynchronizationReport report, Guid sourceServerId, Guid sourceFileETag)
		{
			try
			{
				// we want to execute those operation in a single batch but we also have to ensure that
				// Raven/Synchronization/Sources/sourceServerId config is modified only by one finishing synchronization at the same time
				synchronizationFinishLocks.GetOrAdd(sourceServerId.ToString(), new ReaderWriterLockSlim()).EnterWriteLock();

				Storage.Batch(accessor =>
				{
					SaveSynchronizationReport(fileName, accessor, report);
					FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);

					if (report.Exception == null)
					{
						SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor);
					}
				});
			}
			catch (Exception ex)
			{
				log.ErrorException(
					string.Format("Failed to finish synchronization of a file '{0}' from {1}", fileName, sourceServerId), ex);
			}
			finally
			{
				synchronizationFinishLocks.GetOrAdd(sourceServerId.ToString(), new ReaderWriterLockSlim()).ExitWriteLock();
			}
		}

		private void AssertConflictDetection(string fileName, NameValueCollection localMetadata, NameValueCollection sourceMetadata, Guid sourceServerId, string sourceServerUrl, out bool isConflictResolved)
		{
			var conflict = ConflictDetector.Check(fileName, localMetadata, sourceMetadata, sourceServerUrl);
			isConflictResolved = ConflictResolver.IsResolved(localMetadata, conflict);

			if (conflict != null && !isConflictResolved)
			{
				ConflictArtifactManager.Create(fileName, conflict);

				Publisher.Publish(new ConflictDetected
				                  	{
				                  		FileName = fileName,
				                  		ServerUrl = Request.GetServerUrl()
				                  	});

				log.Debug(
					"File '{0}' is in conflict with synchronized version from {1}. File marked as conflicted, conflict configuration item created",
					fileName, sourceServerId);

				throw new SynchronizationException(string.Format("File {0} is conflicted", fileName));
			}
		}

		private void StartupProceed(string fileName, StorageActionsAccessor accessor)
		{
			// remove previous SyncResult
			DeleteSynchronizationReport(fileName, accessor);

			// remove previous .downloading file
			StorageOperationsTask.IndicateFileToDelete(RavenFileNameHelper.DownloadingFileName(fileName));
		}

		[AcceptVerbs("POST")]
		public HttpResponseMessage UpdateMetadata(string fileName)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault();
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			log.Debug("Starting to update a metadata of file '{0}' with ETag {1} from {2} bacause of synchronization", fileName, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport(fileName, sourceFileETag, SynchronizationType.MetadataUpdate);

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
				});

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Start);
				
				Storage.Batch(accessor => StartupProceed(fileName, accessor));

				var localMetadata = GetLocalMetadata(fileName);
				var sourceMetadata = Request.Headers.FilterHeaders();

				bool isConflictResolved;

				AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, sourceServerUrl, out isConflictResolved);

				Historian.UpdateLastModified(sourceMetadata);

				Storage.Batch(accessor => accessor.UpdateFileMetadata(fileName, sourceMetadata));

				Search.Index(fileName, sourceMetadata);

				if (isConflictResolved)
				{
					ConflictArtifactManager.Delete(fileName);
				}

                PublishFileNotification(fileName, FileChangeAction.Update);
			}
			catch (Exception ex)
			{
				report.Exception = ex;

				log.WarnException(
					string.Format("Error was occured during metadata synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			}
			finally
			{
				FinishSynchronization(fileName, report, sourceServerId, sourceFileETag);

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Finish);
			}

			if (report.Exception == null)
			{
				log.Debug("Metadata of file '{0}' was synchronized successfully from {1}", fileName, sourceServerId);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("DELETE")]
		public HttpResponseMessage Delete(string fileName)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			log.Debug("Starting to delete a file '{0}' with ETag {1} from {2} bacause of synchronization", fileName, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport(fileName, sourceFileETag, SynchronizationType.Delete);

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
				});

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Start);
				
				Storage.Batch(accessor => StartupProceed(fileName, accessor));

				Storage.Batch(accessor =>
				{
					StorageOperationsTask.IndicateFileToDelete(fileName);

					var tombstoneMetadata = new NameValueCollection().WithDeleteMarker();
					Historian.UpdateLastModified(tombstoneMetadata);
					accessor.PutFile(fileName, 0, tombstoneMetadata, true);
				});

                PublishFileNotification(fileName, FileChangeAction.Delete);
			}
			catch (Exception ex)
			{
				report.Exception = ex;

				log.WarnException(
					string.Format("Error was occured during deletion synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			}
			finally
			{
				FinishSynchronization(fileName, report, sourceServerId, sourceFileETag);

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Finish);
			}

			if (report.Exception == null)
			{
				log.Debug("File '{0}' was deleted during synchronization from {1}", fileName, sourceServerId);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage Rename(string fileName, string rename)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault();
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");
			var sourceMetadata = Request.Headers.FilterHeaders();

			log.Debug("Starting to rename a file '{0}' to '{1}' with ETag {2} from {3} because of synchronization", fileName, rename, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport(fileName, sourceFileETag, SynchronizationType.Rename);

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
				});

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Start);
				
				Storage.Batch(accessor => StartupProceed(fileName, accessor));

				var localMetadata = GetLocalMetadata(fileName);

				bool isConflictResolved;

				AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, sourceServerUrl, out isConflictResolved);

				StorageOperationsTask.RenameFile(new RenameFileOperation()
					                                 {
						                                 Name = fileName,
														 Rename = rename,
														 MetadataAfterOperation = sourceMetadata.WithETag(sourceFileETag).DropRenameMarkers()
					                                 });
			}
			catch (Exception ex)
			{
				report.Exception = ex;
				log.WarnException(
					string.Format("Error was occured during renaming synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			
			}
			finally
			{
				FinishSynchronization(fileName, report, sourceServerId, sourceFileETag);

				PublishSynchronizationNotification(fileName, sourceServerId, report.Type, SynchronizationAction.Finish);
			}

			if (report.Exception == null)
			{
				log.Debug("File '{0}' was renamed to '{1}' during synchronization from {2}", fileName, rename, sourceServerId);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("POST")]
		public async Task<IEnumerable<SynchronizationConfirmation>> Confirm()
		{
			var contentStream = await Request.Content.ReadAsStreamAsync();

			var confirmingFiles = new JsonSerializer().Deserialize<IEnumerable<Tuple<string, Guid>>>(new JsonTextReader(new StreamReader(contentStream)));

			return confirmingFiles.Select(file => new SynchronizationConfirmation
				                                                   {
					                                                   FileName = file.Item1,
					                                                   Status = CheckSynchronizedFileStatus(file)
				                                                   });
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Status(string fileName)
		{
			return Request.CreateResponse(HttpStatusCode.OK, GetSynchronizationReport(fileName));
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Finished()
		{
			ListPage<SynchronizationReport> page = null;

			Storage.Batch(accessor =>
			{
				var reports = accessor.GetConfigsWithPrefix<SynchronizationReport>(RavenFileNameHelper.SyncResultNamePrefix, Paging.PageSize * Paging.Start, Paging.PageSize);
				page = new ListPage<SynchronizationReport>(reports, reports.Count);
			});

			return Request.CreateResponse(HttpStatusCode.OK, page);
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Active()
		{
		    return Request.CreateResponse(HttpStatusCode.OK,
		                                  new ListPage<SynchronizationDetails>(
		                                      SynchronizationTask.Queue.Active.Skip(Paging.PageSize*Paging.Start).Take(
		                                          Paging.PageSize), SynchronizationTask.Queue.GetTotalActiveTasks()));
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Pending()
		{
			return Request.CreateResponse(HttpStatusCode.OK,
                                          new ListPage<SynchronizationDetails>(
			                              SynchronizationTask.Queue.Pending.Skip(Paging.PageSize*Paging.Start).Take(
			                              	Paging.PageSize),
                                            SynchronizationTask.Queue.GetTotalPendingTasks()));
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Conflicts()
		{
			ListPage<ConflictItem> page = null;

			Storage.Batch(accessor =>
			{
				var conflicts = accessor.GetConfigsWithPrefix<ConflictItem>(RavenFileNameHelper.ConflictConfigNamePrefix, Paging.PageSize * Paging.Start, Paging.PageSize);
				page = new ListPage<ConflictItem>(conflicts, conflicts.Count);
			});

			return Request.CreateResponse(HttpStatusCode.OK, page);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage ResolveConflict(string fileName, ConflictResolutionStrategy strategy)
		{
			log.Debug("Resolving conflict of a file '{0}' by using {1} strategy", fileName, strategy);

			if (strategy == ConflictResolutionStrategy.CurrentVersion)
			{
				StrategyAsGetCurrent(fileName);
			}
			else
			{
				StrategyAsGetRemote(fileName);
			}

			return new HttpResponseMessage(HttpStatusCode.OK);
		}

		[AcceptVerbs("PATCH")]
		public async Task<HttpResponseMessage> ApplyConflict(string filename, long remoteVersion, string remoteServerId, string remoteServerUrl)
		{
			var localMetadata = GetLocalMetadata(filename);

			if (localMetadata == null)
			{
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}

			var contentStream = await Request.Content.ReadAsStreamAsync();

			var current = new HistoryItem
				              {
					              ServerId = Storage.Id.ToString(),
					              Version = long.Parse(localMetadata[SynchronizationConstants.RavenSynchronizationVersion])
				              };

			var currentConflictHistory = Historian.DeserializeHistory(localMetadata);
			currentConflictHistory.Add(current);

			var remote = new HistoryItem
				             {
					             ServerId = remoteServerId, Version = remoteVersion
				             };

			var remoteConflictHistory =
				new JsonSerializer().Deserialize<IList<HistoryItem>>(new JsonTextReader(new StreamReader(contentStream)));
			remoteConflictHistory.Add(remote);

			var conflict = new ConflictItem
			{
				CurrentHistory = currentConflictHistory,
				RemoteHistory = remoteConflictHistory,
				FileName = filename,
				RemoteServerUrl = Uri.UnescapeDataString(remoteServerUrl)
			};

			ConflictArtifactManager.Create(filename, conflict);

			Publisher.Publish(new ConflictDetected
			{
				FileName = filename,
				ServerUrl = Request.GetServerUrl()
			});

			log.Debug(
				"Conflict applied for a file '{0}' (remote version: {1}, remote server id: {2}).", filename, remoteVersion, remoteServerId);

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage LastSynchronization(Guid from)
		{
			SourceSynchronizationInformation lastEtag = null;
			Storage.Batch(accessor => lastEtag = GetLastSynchronization(from, accessor));

			log.Debug("Got synchronization last ETag request from {0}: [{1}]", from, lastEtag);

			return Request.CreateResponse(HttpStatusCode.OK, lastEtag);
		}

		[AcceptVerbs("POST")]
		public HttpResponseMessage IncrementLastETag(Guid sourceServerId, Guid sourceFileETag)
		{
			try
			{
				// we want to execute those operation in a single batch but we also have to ensure that
				// Raven/Synchronization/Sources/sourceServerId config is modified only by one finishing synchronization at the same time
				synchronizationFinishLocks.GetOrAdd(sourceServerId.ToString(), new ReaderWriterLockSlim()).EnterWriteLock();

				Storage.Batch(accessor => SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor));
			}
			catch (Exception ex)
			{
				log.ErrorException(
					string.Format("Failed to update last seen ETag from {0}", sourceServerId), ex);
			}
			finally
			{
				synchronizationFinishLocks.GetOrAdd(sourceServerId.ToString(), new ReaderWriterLockSlim()).ExitWriteLock();
			}

			return Request.CreateResponse(HttpStatusCode.OK);
		}

	    private void PublishFileNotification(string fileName, FileChangeAction action)
	    {
	        Publisher.Publish(new FileChange()
	                              {
	                                  File = fileName,
	                                  Action = action
	                              });
	    }

	    private void PublishSynchronizationNotification(string fileName, Guid sourceServerId, SynchronizationType type, SynchronizationAction action)
		{
			Publisher.Publish(new SynchronizationUpdate
			{
				FileName = fileName,
				SourceServerId = sourceServerId,
				Type = type,
				Action = action,
				SynchronizationDirection = SynchronizationDirection.Incoming
			});
		}

		private void StrategyAsGetCurrent(string fileName)
		{
			Storage.Batch(accessor =>
			{
				var conflict =
					accessor.GetConfigurationValue<ConflictItem>(RavenFileNameHelper.ConflictConfigNameForFile(fileName));
				var localMetadata = accessor.GetFile(fileName, 0, 0).Metadata;
				var localHistory = Historian.DeserializeHistory(localMetadata);

				// incorporate remote version history into local
				foreach (var remoteHistoryItem in conflict.RemoteHistory.Where(remoteHistoryItem => !localHistory.Contains(remoteHistoryItem)))
				{
					localHistory.Add(remoteHistoryItem);
				}

				localMetadata[SynchronizationConstants.RavenSynchronizationHistory] = Historian.SerializeHistory(localHistory);

				accessor.UpdateFileMetadata(fileName, localMetadata);

				ConflictArtifactManager.Delete(fileName, accessor);
			});
		}

		private void StrategyAsGetRemote(string fileName)
		{
			Storage.Batch(
				accessor =>
				{
					var localMetadata = accessor.GetFile(fileName, 0, 0).Metadata;
					var conflictConfigName = RavenFileNameHelper.ConflictConfigNameForFile(fileName);
					var conflictItem = accessor.GetConfigurationValue<ConflictItem>(conflictConfigName);

					var conflictResolution =
						new ConflictResolution
						{
							Strategy = ConflictResolutionStrategy.RemoteVersion,
							RemoteServerId = conflictItem.RemoteHistory.Last().ServerId,
							Version = conflictItem.RemoteHistory.Last().Version,
						};

					localMetadata[SynchronizationConstants.RavenSynchronizationConflictResolution] =
						new TypeHidingJsonSerializer().Stringify(conflictResolution);
					accessor.UpdateFileMetadata(fileName, localMetadata);
				});
		}

		private FileStatus CheckSynchronizedFileStatus(Tuple<string, Guid> fileInfo)
		{
			var report = GetSynchronizationReport(fileInfo.Item1);

			if (report == null || report.FileETag != fileInfo.Item2)
			{
				return FileStatus.Unknown;
			}

			return report.Exception == null ? FileStatus.Safe : FileStatus.Broken;
		}

		private void SaveSynchronizationReport(string fileName, StorageActionsAccessor accessor, SynchronizationReport report)
		{
			var name = RavenFileNameHelper.SyncResultNameForFile(fileName);
			accessor.SetConfigurationValue(name, report);
		}

		private void DeleteSynchronizationReport(string fileName, StorageActionsAccessor accessor)
		{
			var name = RavenFileNameHelper.SyncResultNameForFile(fileName);
			accessor.DeleteConfig(name);
			Search.Delete(name);
		}

		private SynchronizationReport GetSynchronizationReport(string fileName)
		{
			SynchronizationReport preResult = null;

			Storage.Batch(
				accessor =>
				{
					var name = RavenFileNameHelper.SyncResultNameForFile(fileName);
					accessor.TryGetConfigurationValue(name, out preResult);
				});

			return preResult;
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

			if (result.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker))
			{
				return null;
			}

			return result;
		}

		private SourceSynchronizationInformation GetLastSynchronization(Guid from, StorageActionsAccessor accessor)
		{
			SourceSynchronizationInformation info;
			accessor.TryGetConfigurationValue(SynchronizationConstants.RavenSynchronizationSourcesBasePath + "/" + from, out info);

			return info ?? new SourceSynchronizationInformation()
							{
								LastSourceFileEtag = Guid.Empty,
								DestinationServerInstanceId = Storage.Id
							};
		}

		private void SaveSynchronizationSourceInformation(Guid sourceServerId, Guid lastSourceEtag, StorageActionsAccessor accessor)
		{
			var lastSynchronizationInformation = GetLastSynchronization(sourceServerId, accessor);
			if (Buffers.Compare(lastSynchronizationInformation.LastSourceFileEtag.ToByteArray(), lastSourceEtag.ToByteArray()) > 0)
			{
				return;
			}

			var synchronizationSourceInfo = new SourceSynchronizationInformation
			{
				LastSourceFileEtag = lastSourceEtag,
				DestinationServerInstanceId = Storage.Id
			};

			var key = SynchronizationConstants.RavenSynchronizationSourcesBasePath + "/" + sourceServerId;

			accessor.SetConfigurationValue(key, synchronizationSourceInfo);
			log.Debug("Saved last synchronized file ETag {0} from {1}", lastSourceEtag, sourceServerId);
		}
	}
}
