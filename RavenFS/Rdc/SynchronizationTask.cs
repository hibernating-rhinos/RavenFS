namespace RavenFS.Rdc
{
	using System;
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
		private readonly TransactionalStorage storage;
		private readonly BufferPool bufferPool;
		private readonly ISignatureRepository signatureRepository;
		private readonly SigGenerator sigGenerator;
		private readonly FileLockManager fileLockManager;
		private readonly NotificationPublisher publisher;

		public SynchronizationTask(TransactionalStorage storage, BufferPool bufferPool, FileLockManager fileLockManager, ISignatureRepository signatureRepository, SigGenerator sigGenerator, NotificationPublisher publisher)
		{
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			this.signatureRepository = signatureRepository;
			this.bufferPool = bufferPool;
			this.fileLockManager = fileLockManager;
			this.publisher = publisher;
		}

		public void SynchronizeDestinations(string fileName)
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				if (synchronizationQueue.NumberOfActiveSynchronizationTasksFor(destination) > 1)
				{
					return;
				}

				fileLockManager.LockByCreatingSyncConfiguration(fileName, destination);

				var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

				destinationRavenFileSystemClient.GetMetadataForAsync(fileName)
					.ContinueWith(
						getMetadataForAsyncTask =>
							{
								var destinationMetadata = getMetadataForAsyncTask.Result;

								var localMetadata = GetLocalMetadata(fileName);

								if (destinationMetadata == null)
								{
									// if file doesn't exist on destination server - upload it there
									return UploadToDestination(destinationRavenFileSystemClient, fileName, localMetadata);
								}

								if (destinationMetadata.AllKeys.Contains(SynchronizationConstants.RavenReplicationConflict))
								{
									throw new SynchronizationException(
										string.Format("File {0} on THEIR side is conflicted", fileName));
								}

								var conflict = CheckConflict(localMetadata, destinationMetadata);
								var isConflictResolved = IsConflictResolved(localMetadata, conflict);
								//if (conflict != null && !isConflictResolved)
								//{
								//    CreateConflictArtifacts(fileName, conflict);

								//    publisher.Publish(new ConflictDetected
								//    {
								//        FileName = fileName,
								//        ServerUrl = destination
								//    });

								//    throw new SynchronizationException(string.Format("File {0} is conflicted", fileName));
								//}

								var localFileDataInfo = GetLocalFileDataInfo(fileName);

								var remoteSignatureCache = new VolatileSignatureRepository(TempDirectoryTools.Create());
								var localRdcManager = new LocalRdcManager(signatureRepository, storage, sigGenerator);
								var destinationRdcManager = new RemoteRdcManager(destinationRavenFileSystemClient, signatureRepository, remoteSignatureCache);
								
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
												                   destinationSignatureManifest,
																   sourceSignatureManifest,
												                   localMetadata);
											}
											return UploadToDestination(destinationRavenFileSystemClient, fileName, localMetadata);
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
						fileLockManager.UnlockByDeletingSyncConfiguration(fileName);
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

						storage.Batch(
							accessor =>
							{
								var name = SynchronizationHelper.SyncResultNameForFile(fileName);
								accessor.SetConfigurationValue(name, report);

								//if (task.Status != TaskStatus.Faulted)
								//{
								//    SaveSynchronizationSourceInformation(sourceServerUrl, remoteMetadata.Value<Guid>("ETag"), accessor);
								//}
							});
					});
			}
		}

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, string fileName, SignatureManifest destinationSignatureManifest, SignatureManifest sourceSignatureManifest, NameValueCollection sourceMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository);

			var localFile = StorageStream.Reading(storage, fileName);

			var needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);

			var multipartRequest = new SynchronizationMultipartRequest(destinationServerUrl, fileName, sourceMetadata, localFile, needList);

			return multipartRequest.PushChangesToDesticationAsync()
				.ContinueWith(_ =>
				              	{
				              		localFile.Dispose();
									needListGenerator.Dispose();
				              		_.AssertNotFaulted();

				              		return new SynchronizationReport
				              		       	{
				              		       		FileName = fileName,
				              		       		BytesTransfered = needList.Sum(
				              		       			item =>
				              		       			item.BlockType == RdcNeedType.Source
				              		       				? (long) item.BlockLength
				              		       				: 0L),
				              		       		BytesCopied = needList.Sum(
				              		       			item =>
				              		       			item.BlockType == RdcNeedType.Seed
				              		       				? (long) item.BlockLength
				              		       				: 0L),
				              		       		NeedListLength = needList.Count
				              		       	};
				              	});
		}

		private Task<SynchronizationReport> UploadToDestination(RavenFileSystemClient destinationRavenFileSystemClient, string fileName, NameValueCollection localMetadata)
		{
			var ravenReadOnlyStream = new RavenReadOnlyStream(storage, bufferPool, fileName);
			
			return destinationRavenFileSystemClient.UploadAsync(fileName, localMetadata, ravenReadOnlyStream)
				.ContinueWith(
					uploadAsyncTask =>
						{
							ravenReadOnlyStream.Dispose();
							uploadAsyncTask.AssertNotFaulted();

							return new SynchronizationReport
							{
								FileName = fileName,
								BytesCopied = StorageStream.Reading(storage, fileName).Length
							};
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
			NameValueCollection destionationsConfig = new NameValueCollection();

			//storage.Batch(accessor => accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationDestinations, out destinations));
			storage.Batch(accessor => destionationsConfig = accessor.GetConfig(SynchronizationConstants.RavenReplicationDestinations));

			string[] destinations = destionationsConfig["url"].Split(',');

			return destinations;
		}
	}
}