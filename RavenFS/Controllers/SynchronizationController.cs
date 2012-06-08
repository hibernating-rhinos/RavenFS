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

	public class SynchronizationController : RavenController
	{
		[AcceptVerbs("POST")]
		public Task<IEnumerable<DestinationSyncResult>> ToDestinations()
		{
			var synchronizeDestinationTasks = SynchronizationTask.SynchronizeDestinationsAsync().Result;

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
			return SynchronizationTask.PerformSynchronization(destinationServerUrl, new ContentUpdateWorkItem(fileName, RavenFileSystem.ServerUrl, Storage, SigGenerator));
		}

		[AcceptVerbs("POST")]
		public Task<HttpResponseMessage> MultipartProceed()
		{
			if (!Request.Content.IsMimeMultipartContent())
			{
				throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
			}

			string fileName = Request.Headers.GetValues(SyncingMultipartConstants.FileName).FirstOrDefault();
			string tempFileName = SynchronizationHelper.DownloadingFileName(fileName);

			string sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerUrl).FirstOrDefault();
			Guid lastEtagFromSource = Request.Headers.Value<Guid>("ETag");

			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(fileName, accessor);
				StartupProceed(fileName, accessor);
				FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerUrl, accessor);
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

									var multipartProcessor = new SynchronizationMultipartProcessor(fileName,
																								   multipartReadTask.Result.Contents
																								   .GetEnumerator(),
																								   localFile,
																								   synchronizingFile);

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
														})
										.ContinueWith(
											task =>
											{
												Storage.Batch(accessor => FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor));
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
																Exception = task.Exception.ExtractSingleInnerException(),
																Type = SynchronizationType.ContentUpdate
															};
												}
												else
												{
													report = task.Result;
												}
												Storage.Batch(
													accessor =>
													{
														SaveSynchronizationReport(fileName, accessor, report);

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
															return Request.CreateResponse(HttpStatusCode.OK, task.Result);
														});
								}).Unwrap();
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
			var sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerUrl).FirstOrDefault();
			var lastEtagFromSource = Request.Headers.Value<Guid>("ETag");

			var report = new SynchronizationReport
			             	{
			             		Type = SynchronizationType.MetadataUpdate
			             	};

			try
			{
				Storage.Batch(accessor =>
				{
				    AssertFileIsNotBeingSynced(fileName, accessor);
				    StartupProceed(fileName, accessor);
				    FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerUrl, accessor);
				});

				var headers = Request.Headers.FilterHeaders();
				HistoryUpdater.UpdateLastModified(headers);
				HistoryUpdater.Update(fileName, headers);

				Storage.Batch(accessor => accessor.UpdateFileMetadata(fileName, headers));

				Search.Index(fileName, headers);
			}
			catch (Exception ex)
			{
				report.Exception = ex;
			}
			finally
			{
				Storage.Batch(accessor =>
				{
				    FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);
					SaveSynchronizationReport(fileName, accessor, report);

					if (report.Exception != null)
					{
						SaveSynchronizationSourceInformation(sourceServerUrl, lastEtagFromSource, accessor);
					}
				});
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		[AcceptVerbs("DELETE")]
		public HttpResponseMessage Delete(string fileName)
		{
			var sourceServerUrl = Request.Headers.GetValues(SyncingMultipartConstants.SourceServerUrl).FirstOrDefault();
			var lastEtagFromSource = Request.Headers.Value<Guid>("ETag");

			var report = new SynchronizationReport
			{
				Type = SynchronizationType.Deletion
			};

			try
			{
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(fileName, accessor);
					StartupProceed(fileName, accessor);
					FileLockManager.LockByCreatingSyncConfiguration(fileName, sourceServerUrl, accessor);
				});

				Storage.Batch(accessor => accessor.Delete(fileName));

				Search.Delete(fileName);
			}
			catch (Exception ex)
			{
				report.Exception = ex;
			}
			finally
			{
				Storage.Batch(accessor =>
				{
					FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);
					SaveSynchronizationReport(fileName, accessor, report);

					if (report.Exception != null)
					{
						SaveSynchronizationSourceInformation(sourceServerUrl, lastEtagFromSource, accessor);
					}
				});
			}

			return Request.CreateResponse(HttpStatusCode.OK, report);
		}

		//[AcceptVerbs("PATCH")]
		//public HttpResponseMessage Rename(string fileName)
		//{
		//    return Request.CreateResponse(HttpStatusCode.OK, task.Result);
		//}

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
		public HttpResponseMessage Finished(int page, int pageSize)
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
						(from item in configKeys.Skip(pageSize * page).Take(pageSize)
						 select accessor.GetConfigurationValue<SynchronizationReport>(item)).ToList();
				});
			return Request.CreateResponse(HttpStatusCode.OK, configObjects);
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Active(int page, int pageSize)
		{
			return Request.CreateResponse(HttpStatusCode.OK, SynchronizationTask.Queue.Active.Skip(pageSize * page).Take(pageSize));
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Pending(int page, int pageSize)
		{
			return Request.CreateResponse(HttpStatusCode.OK, SynchronizationTask.Queue.Pending.Skip(pageSize * page).Take(pageSize));
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
		public HttpResponseMessage LastSynchronization(string from)
		{
			SourceSynchronizationInformation lastEtag = null;
			Storage.Batch(accessor => lastEtag = GetLastSynchronization(StringUtils.RemoveTrailingSlashAndEncode(from), accessor));
			return Request.CreateResponse(HttpStatusCode.OK, lastEtag);
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

		private SourceSynchronizationInformation GetLastSynchronization(string from, StorageActionsAccessor accessor)
		{
			SourceSynchronizationInformation info;
			accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationSourcesBasePath + "/" + from, out info);

			return info ?? new SourceSynchronizationInformation()
							{
								LastSourceFileEtag = Guid.Empty,
								DestinationServerInstanceId = Storage.Id
							};
		}

		private void SaveSynchronizationSourceInformation(string sourceServerUrl, Guid lastSourceEtag, StorageActionsAccessor accessor)
		{
			var lastSynchronizationInformation = GetLastSynchronization(StringUtils.RemoveTrailingSlashAndEncode(sourceServerUrl), accessor);
			if (Buffers.Compare(lastSynchronizationInformation.LastSourceFileEtag.ToByteArray(), lastSourceEtag.ToByteArray()) > 0)
			{
				return;
			}

			var synchronizationSourceInfo = new SourceSynchronizationInformation
			{
				LastSourceFileEtag = lastSourceEtag,
				DestinationServerInstanceId = Storage.Id
			};

			var key = SynchronizationConstants.RavenReplicationSourcesBasePath + "/" + StringUtils.RemoveTrailingSlashAndEncode(sourceServerUrl);

			accessor.SetConfigurationValue(key, synchronizationSourceInfo);
		}
	}
}
