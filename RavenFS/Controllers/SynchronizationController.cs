namespace RavenFS.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
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
	using ConflictDetected = Notifications.ConflictDetected;
	using SynchronizationAction = Notifications.SynchronizationAction;
	using SynchronizationDirection = Notifications.SynchronizationDirection;
	using SynchronizationUpdate = Notifications.SynchronizationUpdate;

	public class SynchronizationController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		[AcceptVerbs("POST")]
		public Task<IEnumerable<DestinationSyncResult>> ToDestinations()
		{
			var synchronizeDestinationTasks = SynchronizationTask.SynchronizeDestinationsAsync(forceSyncingContinuation:false).Result;

			if (synchronizeDestinationTasks.Length > 0)
			{
				return Task.Factory.ContinueWhenAll(synchronizeDestinationTasks,
				                                    t => t.Select(destinationTasks => destinationTasks.Result));
			}

			return new CompletedTask<IEnumerable<DestinationSyncResult>>(Enumerable.Empty<DestinationSyncResult>());
		}

		[AcceptVerbs("POST")]
		public Task<SynchronizationReport> Start(string fileName, string destinationServerUrl)
		{
			log.Debug("Starting to synchronize a file '{0}' to {1}", fileName, destinationServerUrl);

			return SynchronizationTask.SynchronizeFileTo(fileName, destinationServerUrl);
		}

		[AcceptVerbs("POST")]
		public Task<HttpResponseMessage> MultipartProceed()
		{
			if (!Request.Content.IsMimeMultipartContent())
			{
				throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
			}

			var fileName = Request.Headers.GetValues(SyncingMultipartConstants.FileName).FirstOrDefault();
			var tempFileName = SynchronizationHelper.DownloadingFileName(fileName);

			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			log.Debug("Starting to process multipart synchronization request of a file '{0}' with ETag {1} from {2}", fileName, sourceFileETag, sourceServerId);

			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(fileName, accessor);
				StartupProceed(fileName, accessor);
				FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
			});

			StorageStream localFile = null;
			StorageStream synchronizingFile = null;

			bool isConflictResolved = false;

			var sourceMetadata = Request.Headers.FilterHeaders();

			return Request.Content.ReadAsMultipartAsync()
				.ContinueWith(multipartReadTask =>
				{
					var localMetadata = GetLocalMetadata(fileName);

					if (localMetadata != null)
					{
						AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, out isConflictResolved);

						localFile = StorageStream.Reading(Storage, fileName);
					}

					HistoryUpdater.UpdateLastModified(sourceMetadata);

					synchronizingFile = StorageStream.CreatingNewAndWritting(Storage, Search, tempFileName, sourceMetadata);

					var multipartProcessor = new SynchronizationMultipartProcessor(fileName,
					                                                               multipartReadTask.Result.Contents.GetEnumerator(),
					                                                               localFile, synchronizingFile);

					return multipartProcessor.ProcessAsync()
						.ContinueWith(task =>
						{
							if (synchronizingFile != null)
							{
								synchronizingFile.Dispose();
							}

							if (localFile != null)
							{
								localFile.Dispose();
							}

							task.AssertNotFaulted();

							using (var stream = StorageStream.Reading(Storage, tempFileName))
							{
								sourceMetadata["Content-MD5"] = stream.GetMD5Hash();
								Storage.Batch(accesor => accesor.UpdateFileMetadata(tempFileName, sourceMetadata));
							}

							Storage.Batch(
								accessor =>
								{
									accessor.Delete(fileName);
									accessor.RenameFile(tempFileName, fileName);

									Search.Delete(tempFileName);
									Search.Index(fileName, sourceMetadata);
								});

							if (isConflictResolved)
							{
								ConflictActifactManager.RemoveArtifact(fileName);
							}

							return task.Result;
						});
				}).Unwrap()
				.ContinueWith(
					task =>
					{
						SynchronizationReport report;
						if (task.Status == TaskStatus.Faulted)
						{
							var exception = task.Exception.ExtractSingleInnerException();
							if (exception is HttpRequestException)
							{
								exception = exception.InnerException;
							}

							report =
								new SynchronizationReport
									{
										FileName = fileName,
										Exception = exception,
										Type = SynchronizationType.ContentUpdate
									};
						}
						else
						{
							report = task.Result;
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
								              sourceServerId),
								report.Exception);
						}

						Storage.Batch(
							accessor =>
							{
								SaveSynchronizationReport(fileName, accessor, report);
								FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);

								if (task.Status != TaskStatus.Faulted)
								{
									SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor);
								}
							});

						PublishSynchronizationFinishedNotification(fileName, sourceServerId, report.Type);

						return Request.CreateResponse(HttpStatusCode.OK, report);
					});
		}

		private void AssertConflictDetection(string fileName, NameValueCollection destinationMetadata, NameValueCollection sourceMetadata, Guid sourceServerId, out bool isConflictResolved)
		{
			var conflict = ConflictDetector.Check(destinationMetadata, sourceMetadata);
			isConflictResolved = ConflictResolver.IsResolved(destinationMetadata, conflict);

			if (conflict != null && !isConflictResolved)
			{
				ConflictActifactManager.CreateArtifact(fileName, conflict);

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
			accessor.Delete(SynchronizationHelper.DownloadingFileName(fileName));
		}

		[AcceptVerbs("POST")]
		public HttpResponseMessage UpdateMetadata(string fileName)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			log.Debug("Starting to update a metadata of file '{0}' with ETag {1} from {2} bacause of synchronization", fileName, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport
			             	{
								FileName = fileName,
			             		Type = SynchronizationType.MetadataUpdate
			             	};

			
			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(fileName, accessor);
				StartupProceed(fileName, accessor);
				FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
			});

			try
			{
				var localMetadata = GetLocalMetadata(fileName);
				var sourceMetadata = Request.Headers.FilterHeaders();

				bool isConflictResolved;

				AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, out isConflictResolved);

				HistoryUpdater.UpdateLastModified(sourceMetadata);

				Storage.Batch(accessor => accessor.UpdateFileMetadata(fileName, sourceMetadata));

				Search.Index(fileName, sourceMetadata);

				if (isConflictResolved)
				{
					ConflictActifactManager.RemoveArtifact(fileName);
				}
			}
			catch (Exception ex)
			{
				report.Exception = ex;

				log.WarnException(
					string.Format("Error was occured during metadata synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			}
			finally
			{
				Storage.Batch(accessor =>
				{
					SaveSynchronizationReport(fileName, accessor, report);
					FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);

					if (report.Exception == null)
					{
						log.Debug("Metadata of file '{0}' was synchronized successfully from {1}", fileName, sourceServerId);	

						SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor);
					}
				});

				PublishSynchronizationFinishedNotification(fileName, sourceServerId, report.Type);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("DELETE")]
		public HttpResponseMessage Delete(string fileName)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");

			log.Debug("Starting to delete a file '{0}' with ETag {1} from {2} bacause of synchronization", fileName, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport
			{
				FileName = fileName,
				Type = SynchronizationType.Delete
			};

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					StartupProceed(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
				});

				Storage.Batch(accessor => accessor.Delete(fileName));

				Search.Delete(fileName);
			}
			catch (Exception ex)
			{
				report.Exception = ex;

				log.WarnException(
					string.Format("Error was occured during deletion synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			}
			finally
			{
				Storage.Batch(accessor =>
				{
					SaveSynchronizationReport(fileName, accessor, report);
					FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);

					if (report.Exception == null)
					{
						log.Debug("File '{0}' was deleted during synchronization from {1}", fileName, sourceServerId);	

						SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor);
					}
				});

				PublishSynchronizationFinishedNotification(fileName, sourceServerId, report.Type);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage Rename(string fileName, string rename)
		{
			var sourceServerId = new Guid(Request.Headers.GetValues(SyncingMultipartConstants.SourceServerId).FirstOrDefault());
			var sourceFileETag = Request.Headers.Value<Guid>("ETag");
			var sourceMetadata = Request.Headers.FilterHeaders();

			log.Debug("Starting to rename a file '{0}' to '{1}' with ETag {2} from {3} because of synchronization", fileName, rename, sourceServerId, sourceFileETag);

			var report = new SynchronizationReport
			{
				FileName = fileName,
				Type = SynchronizationType.Rename
			};

			
			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(fileName, accessor);
				StartupProceed(fileName, accessor);
				FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerId, accessor);
			});

			try
			{
				var localMetadata = GetLocalMetadata(fileName);

				bool isConflictResolved;

				AssertConflictDetection(fileName, localMetadata, sourceMetadata, sourceServerId, out isConflictResolved);

				FileAndPages fileAndPages = null;
				Storage.Batch(accessor =>
				{
					fileAndPages = accessor.GetFile(fileName, 0, 0);
					accessor.RenameFile(fileName, rename);
				});

				Search.Delete(fileName);
				Search.Index(rename, fileAndPages.Metadata);
			}
			catch (Exception ex)
			{
				report.Exception = ex;
				log.WarnException(
					string.Format("Error was occured during renaming synchronization of file '{0}' from {1}", fileName, sourceServerId), ex);
			
			}
			finally
			{
				Storage.Batch(accessor =>
				{
					SaveSynchronizationReport(fileName, accessor, report);
					FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);

					if (report.Exception == null)
					{
						log.Debug("File '{0}' was renamed to '{1}' during synchronization from {2}", fileName, rename, sourceServerId);	

						SaveSynchronizationSourceInformation(sourceServerId, sourceFileETag, accessor);
					}
				});

				PublishSynchronizationFinishedNotification(fileName, sourceServerId, report.Type);
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("POST")]
		public Task<IEnumerable<SynchronizationConfirmation>> Confirm()
		{
			return Request.Content.ReadAsStreamAsync().
				ContinueWith(t =>
								{

									var confirmingFiles =
										new JsonSerializer().Deserialize<string[]>(new JsonTextReader(new StreamReader(t.Result)));

									var confirmations = confirmingFiles.Select(file => new SynchronizationConfirmation
																						{
																							FileName = file,
																							Status = CheckSynchronizedFileStatus(file)
																						}).ToList();

									return confirmations.AsEnumerable();
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
			IList<SynchronizationReport> configObjects = null;
			Storage.Batch(
				accessor =>
				{
					var configKeys =
						from item in accessor.GetConfigNames()
						where SynchronizationHelper.IsSyncResultName(item)
						select item;
					configObjects =
						(from item in configKeys.Skip(Paging.PageSize * Paging.Start).Take(Paging.PageSize)
						 select accessor.GetConfigurationValue<SynchronizationReport>(item)).ToList();
				});
			return Request.CreateResponse(HttpStatusCode.OK, configObjects);
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Active()
		{
			return Request.CreateResponse(HttpStatusCode.OK,
			                              SynchronizationTask.Queue.Active.Skip(Paging.PageSize*Paging.Start).Take(
			                              	Paging.PageSize));
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Pending()
		{
			return Request.CreateResponse(HttpStatusCode.OK,
			                              SynchronizationTask.Queue.Pending.Skip(Paging.PageSize*Paging.Start).Take(
			                              	Paging.PageSize));
		}

		[AcceptVerbs("PATCH")]
		public Task<HttpResponseMessage> ResolveConflict(string fileName, ConflictResolutionStrategy strategy, string sourceServerUrl)
		{
			log.Debug("Resolving conflict for file '{0}' with {1} version as using {1} strategy", fileName, sourceServerUrl,
			          strategy);

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
						long.Parse(localMetadata[SynchronizationConstants.RavenSynchronizationVersion])
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

			log.Debug(
				"Conflict applied for a file '{0}' (remote version: {1}, remote server id: {2}).", filename, remoteVersion, remoteServerId);

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("PATCH")]
		public Task<HttpResponseMessage> ResolveConflictInFavorOfDest(string filename, long remoteVersion, string remoteServerId)
		{
			ApplyConflict(filename, remoteVersion, remoteServerId);

			return ResolveConflict(filename, ConflictResolutionStrategy.RemoteVersion, Request.GetServerUrl());
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage LastSynchronization(Guid from)
		{
			SourceSynchronizationInformation lastEtag = null;
			Storage.Batch(accessor => lastEtag = GetLastSynchronization(from, accessor));

			log.Debug("Got synchronization last etag request from {0}: [{1}]", from, lastEtag);

			return Request.CreateResponse(HttpStatusCode.OK, lastEtag);
		}

		private void PublishSynchronizationFinishedNotification(string fileName, Guid sourceServerId, SynchronizationType type)
		{
			Publisher.Publish(new SynchronizationUpdate
			{
				FileName = fileName,
				SourceServerId = sourceServerId,
				Type = type,
				Action = SynchronizationAction.Finish,
				SynchronizationDirection = SynchronizationDirection.Incoming
			});
		}

		private Task StrategyAsGetCurrent(string fileName, string sourceServerUrl)
		{
			var sourceRavenFileSystemClient = new RavenFileSystemClient(sourceServerUrl);
			ConflictActifactManager.RemoveArtifact(fileName);
			var localMetadata = GetLocalMetadata(fileName);
			var version = long.Parse(localMetadata[SynchronizationConstants.RavenSynchronizationVersion]);
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
					localMetadata[SynchronizationConstants.RavenSynchronizationConflictResolution] =
						new TypeHidingJsonSerializer().Stringify(conflictResolution);
					accessor.UpdateFileMetadata(fileName, localMetadata);
				});
		}

		private FileStatus CheckSynchronizedFileStatus(string fileName)
		{
			var report = GetSynchronizationReport(fileName);

			if (report == null)
			{
				return FileStatus.Unknown;
			}

			return report.Exception == null ? FileStatus.Safe : FileStatus.Broken;
		}

		private void SaveSynchronizationReport(string fileName, StorageActionsAccessor accessor, SynchronizationReport report)
		{
			var name = SynchronizationHelper.SyncResultNameForFile(fileName);
			accessor.SetConfigurationValue(name, report);
		}

		private void DeleteSynchronizationReport(string fileName, StorageActionsAccessor accessor)
		{
			var name = SynchronizationHelper.SyncResultNameForFile(fileName);
			accessor.DeleteConfig(name);
			Search.Delete(name);
		}

		private SynchronizationReport GetSynchronizationReport(string fileName)
		{
			SynchronizationReport preResult = null;

			Storage.Batch(
				accessor =>
				{
					var name = SynchronizationHelper.SyncResultNameForFile(fileName);
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
