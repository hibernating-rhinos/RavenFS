namespace RavenFS.Rdc
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Infrastructure;
	using Multipart;
	using Notifications;
	using Storage;
	using Util;
	using Wrapper;
	using ConflictDetected = Notifications.ConflictDetected;

	public class SynchronizationTask
	{
		private readonly SynchronizationQueue synchronizationQueue = new SynchronizationQueue();
		private readonly RavenFileSystem localRavenFileSystem;
		private readonly TransactionalStorage storage;
		private readonly ISignatureRepository signatureRepository;
		private readonly SigGenerator sigGenerator;
		private readonly NotificationPublisher publisher;

		public SynchronizationTask(RavenFileSystem localRavenFileSystem, TransactionalStorage storage, ISignatureRepository signatureRepository, SigGenerator sigGenerator, NotificationPublisher publisher)
		{
			this.localRavenFileSystem = localRavenFileSystem;
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			this.signatureRepository = signatureRepository;
			this.publisher = publisher;
		}

		private string UrlEncodedServerUrl()
		{
			var result = localRavenFileSystem.ServerUrl;
			while (result.EndsWith("/"))
				result = result.Substring(0, result.Length - 1);

			return Uri.EscapeDataString(result);
		}

		public void SynchronizeDestinations(string fileName)
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				StartSyncingToAsync(fileName, destination);
			}
		}

		public Task<SynchronizationReport> StartSyncingToAsync(string fileName, string destination)
		{
			if (synchronizationQueue.NumberOfActiveSynchronizationTasksFor(destination) > 1)
			{
				return new Task<SynchronizationReport>(() => new SynchronizationReport());
			}

			var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

			return destinationRavenFileSystemClient.GetMetadataForAsync(fileName)
				.ContinueWith(
					getMetadataForAsyncTask =>
						{
							NameValueCollection destinationMetadata = null;

							if (!getMetadataForAsyncTask.IsFaulted)
							{
								destinationMetadata = getMetadataForAsyncTask.Result;
							}

							var sourceMetadata = GetLocalMetadata(fileName);

							if (destinationMetadata == null)
							{
								// if file doesn't exist on destination server - upload it there
								return UploadTo(destination, fileName, sourceMetadata);
							}

							if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenReplicationConflict))
							{
								throw new SynchronizationException(
									string.Format("File {0} you want to synchronize is conflicted", fileName));
							}

							var conflict = CheckConflict(sourceMetadata, destinationMetadata);
							var destinationConflictResolutionStrategy = GetConflictResolutionStrategy(destinationMetadata);

							if (conflict != null)
							{
								if (destinationConflictResolutionStrategy != null)
								{
									if (IsConflictResolvedInFavorOfDestination(destinationConflictResolutionStrategy))
									{
										CreateConflictArtifacts(fileName, conflict);

										publisher.Publish(new ConflictDetected
										                  	{
										                  		FileName = fileName,
										                  		ServerUrl = destination
										                  	});

										throw new SynchronizationException(string.Format("File {0} is conflicted", fileName));
									}
									else if (!IsConflictResolvedInFavorOfSource(conflict, destinationConflictResolutionStrategy))
									{
										// if conflict is resolved in favor of source we can continue 
										// but we just want to be sure that proper resolution is applied
										throw new SynchronizationException(
											string.Format("Invalid conflict resolution strategy on {0}", destination));
									}
								}
								else
								{
									destinationRavenFileSystemClient.Synchronization.ApplyConflictAsync(fileName, conflict.Ours.Version,
									                                                                    conflict.Ours.ServerId);
									throw new SynchronizationException(
										string.Format("File {0} is conflicted. No resolution provided by {1}.", fileName, destination));
								}
							}

							var localFileDataInfo = GetLocalFileDataInfo(fileName);

							var remoteSignatureCache = new VolatileSignatureRepository(TempDirectoryTools.Create());
							var localRdcManager = new LocalRdcManager(signatureRepository, storage, sigGenerator);
							var destinationRdcManager = new RemoteRdcManager(destinationRavenFileSystemClient, signatureRepository,
							                                                 remoteSignatureCache);

							var sourceSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);

							return destinationRdcManager.SynchronizeSignaturesAsync(localFileDataInfo)
								.ContinueWith(
									task =>
										{
											var destinationSignatureManifest = task.Result;

											if (destinationSignatureManifest.Signatures.Count > 0)
											{
												return SynchronizeTo(remoteSignatureCache, destination,
												                     fileName,
												                     sourceSignatureManifest,
												                     sourceMetadata);
											}
											return UploadTo(destination, fileName, sourceMetadata);
										})
								.Unwrap()
								.ContinueWith(
									synchronizationTask =>
										{
											remoteSignatureCache.Dispose();

											return synchronizationTask.Result;
										});
						})
				.Unwrap()
				.ContinueWith(task =>
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

									storage.Batch(
										accessor =>
										{
											var name = SynchronizationHelper.SyncResultNameForFile(fileName);
											accessor.SetConfigurationValue(name, report);
										});

				              		return task.Result;
				              	});
		}

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, string fileName, SignatureManifest sourceSignatureManifest, NameValueCollection sourceMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository);

			var localFile = StorageStream.Reading(storage, fileName);

			var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, sourceMetadata, localFile, needList, localFile,
			                                   needListGenerator);
		}

		private Task<SynchronizationReport> UploadTo(string destinationServerUrl, string fileName, NameValueCollection localMetadata)
		{
			var sourceFileStream = StorageStream.Reading(storage, fileName);
			var fileSize = sourceFileStream.Length;

			var onlySourceNeed = new List<RdcNeed>
			               	{
			               		new RdcNeed
			               			{
			               				BlockType = RdcNeedType.Source,
			               				BlockLength = (ulong) fileSize,
			               				FileOffset = 0
			               			}
			               	};

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, localMetadata, sourceFileStream, onlySourceNeed,  sourceFileStream);
		}

		private Task<SynchronizationReport> PushByUsingMultipartRequest(string destinationServerUrl, string fileName, NameValueCollection sourceMetadata, Stream sourceFileStream, IList<RdcNeed> needList, params IDisposable[] disposables)
		{
			var multipartRequest = new SynchronizationMultipartRequest(destinationServerUrl, UrlEncodedServerUrl(), fileName, sourceMetadata,
																	   sourceFileStream, needList);

			return multipartRequest.PushChangesAsync()
				.ContinueWith(t =>
				{
					foreach (var disposable in disposables)
					{
						disposable.Dispose();
					}

					t.AssertNotFaulted();

					return t.Result;
				});
		}

		private void CreateConflictArtifacts(string fileName, ConflictItem conflict)
		{
			storage.Batch(
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
			storage.Batch(
				accessor =>
				{
					accessor.DeleteConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName));
					var metadata = accessor.GetFile(fileName, 0, 0).Metadata;
					metadata.Remove(SynchronizationConstants.RavenReplicationConflict);
					metadata.Remove(SynchronizationConstants.RavenReplicationConflictResolution);
					accessor.UpdateFileMetadata(fileName, metadata);
				});
		}

		private ConflictItem CheckConflict(NameValueCollection sourceMetadata, NameValueCollection destinationMetadata)
		{
			var localHistory = HistoryUpdater.DeserializeHistory(sourceMetadata);
			var remoteVersion = long.Parse(destinationMetadata[SynchronizationConstants.RavenReplicationVersion]);
			var remoteServerId = destinationMetadata[SynchronizationConstants.RavenReplicationSource];
			var localVersion = long.Parse(sourceMetadata[SynchronizationConstants.RavenReplicationVersion]);
			var localServerId = sourceMetadata[SynchronizationConstants.RavenReplicationSource];
			// if there are the same files or destination is direct child there are no conflicts
			if ((remoteServerId == localServerId && remoteVersion == localVersion)
				|| localHistory.Any(item => item.ServerId == remoteServerId && item.Version == remoteVersion))
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

		private ConflictResolution GetConflictResolutionStrategy(NameValueCollection metadata)
		{
			var conflictResolutionString = metadata[SynchronizationConstants.RavenReplicationConflictResolution];
			if (String.IsNullOrEmpty(conflictResolutionString))
			{
				return null;
			}
			return new TypeHidingJsonSerializer().Parse<ConflictResolution>(conflictResolutionString);
		}

		private bool IsConflictResolvedInFavorOfSource(ConflictItem conflict, ConflictResolution destinationConflictResolution)
		{
			return destinationConflictResolution.Strategy == ConflictResolutionStrategy.RemoteVersion
				&& destinationConflictResolution.TheirServerId == conflict.Ours.ServerId;
		}

		private bool IsConflictResolvedInFavorOfDestination(ConflictResolution destinationConflictResolution)
		{
			return destinationConflictResolution.Strategy == ConflictResolutionStrategy.CurrentVersion;
		}

		private NameValueCollection GetLocalMetadata(string fileName)
		{
			NameValueCollection result = null;
			try
			{
				storage.Batch(
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
				storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
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

		private string[] GetSynchronizationDestinations()
		{
			var destionationsConfig = new NameValueCollection();

			//storage.Batch(accessor => accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationDestinations, out destinations));
			storage.Batch(accessor => destionationsConfig = accessor.GetConfig(SynchronizationConstants.RavenReplicationDestinations));

			string[] destinations = destionationsConfig["url"].Split(',');

			return destinations;
		}
	}
}