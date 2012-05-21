namespace RavenFS.Rdc
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Reactive.Linq;
	using System.Threading.Tasks;
	using Client;
	using Conflictuality;
	using Extensions;
	using Infrastructure;
	using Multipart;
	using Storage;
	using Util;
	using Wrapper;

	public class SynchronizationTask
	{
		private readonly SynchronizationQueue synchronizationQueue;
		private readonly RavenFileSystem localRavenFileSystem;
		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;
		private readonly ConflictActifactManager conflictActifactManager;
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(10));

		public SynchronizationTask(RavenFileSystem localRavenFileSystem, TransactionalStorage storage, SigGenerator sigGenerator, ConflictActifactManager conflictActifactManager, ConflictDetector conflictDetector, ConflictResolver conflictResolver)
		{
			this.localRavenFileSystem = localRavenFileSystem;
			this.conflictResolver = conflictResolver;
			this.conflictDetector = conflictDetector;
			this.conflictActifactManager = conflictActifactManager;
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			synchronizationQueue = new SynchronizationQueue(storage);

			InitializeTimer();
		}

		public SynchronizationQueue Queue
		{
			get { return synchronizationQueue; }
		}

		public void InitializeTimer()
		{
			timer.Subscribe(tick => SynchronizeDestinations());
		}

		public void SynchronizeDestinations()
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				string destinationUrl = destination;

				if (!synchronizationQueue.CanSynchronizeTo(destinationUrl))
				{
					continue;
				}

				var destinationClient = new RavenFileSystemClient(destinationUrl);

				destinationClient.Synchronization.GetLastSynchronizationFromAsync(localRavenFileSystem.ServerUrl)
					.ContinueWith(etagTask =>
					{
						etagTask.AssertNotFaulted();

						var filesToSynchronization = GetFilesToSynchronization(etagTask, 100);

						foreach (var fileHeader in filesToSynchronization)
						{
							synchronizationQueue.EnqueueSynchronization(destinationUrl, fileHeader.Name);
						}

						var filesNeedConfirmation = GetSyncingConfigurations(destinationUrl);

						ConfirmPushedFiles(filesNeedConfirmation, destinationClient)
							.ContinueWith(confirmationTask =>
							{
								confirmationTask.AssertNotFaulted();

								foreach (var confirmation in confirmationTask.Result)
								{
									if (confirmation.Status == FileStatus.Safe)
									{
										RemoveSyncingConfiguration(confirmation.FileName, destinationUrl);
									}
									else
									{
										synchronizationQueue.EnqueueSynchronization(destinationUrl, confirmation.FileName);
									}
								}
							})
							.ContinueWith(t =>
							{
								t.AssertNotFaulted();

								SynchronizePendingFiles(destinationUrl);
							});
					});
			}
		}

		private void SynchronizePendingFiles(string destinationUrl)
		{
			for (var i = 0; i < synchronizationQueue.AvailableSynchronizationRequestsTo(destinationUrl); i++)
			{
				string fileName;
				if (synchronizationQueue.TryDequeuePendingSynchronization(destinationUrl, out fileName))
				{
					StartSyncingToAsync(fileName, destinationUrl)
						.ContinueWith(syncingTask => SynchronizePendingFiles(destinationUrl));
				}
				else
				{
					break;
				}
			}
		}

		public Task<SynchronizationReport> StartSyncingToAsync(string fileName, string destination)
		{
			if (!synchronizationQueue.CanSynchronizeTo(destination))
			{
				return SynchronizationExceptionReport(string.Format("The limit of active synchronizations to {0} server has been achieved.",
																 destination));
			}

			var sourceMetadata = GetLocalMetadata(fileName);

			if(sourceMetadata == null)
				return SynchronizationExceptionReport(string.Format("File {0} could not be found", fileName));

			if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenReplicationConflict))
			{
				return SynchronizationExceptionReport(string.Format("File {0} is conflicted", fileName));
			}

			var fileEtag = sourceMetadata.Value<Guid>("ETag");

			synchronizationQueue.SynchronizationStarted(fileName, fileEtag, destination);

			var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

			return destinationRavenFileSystemClient.GetMetadataForAsync(fileName)
				.ContinueWith(
					getMetadataForAsyncTask =>
						{
							var destinationMetadata = getMetadataForAsyncTask.Result;

							if (destinationMetadata == null)
							{
								// if file doesn't exist on destination server - upload it there
								return UploadTo(destination, fileName, sourceMetadata);
							}

							var conflict = conflictDetector.Check(destinationMetadata, sourceMetadata);
							var isConflictResolved = conflictResolver.IsResolved(destinationMetadata, conflict);

							// optimization - conflict checking on source side before any changes pushed
							if (conflict != null && !isConflictResolved)
							{
								return destinationRavenFileSystemClient.Synchronization
									.ApplyConflictAsync(fileName, conflict.Current.Version,conflict.Remote.ServerId)
									.ContinueWith(task =>
									{
										task.AssertNotFaulted();
										return new SynchronizationReport()
										{
											Exception = new SynchronizationException(string.Format("File {0} is conflicted.", fileName))
										};
									});
								
							}

							var localFileDataInfo = GetLocalFileDataInfo(fileName);

							var signatureRepository = new StorageSignatureRepository(storage, fileName);
							var remoteSignatureCache = new VolatileSignatureRepository(fileName);
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
											signatureRepository.Dispose();
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
										
										if(task.Result.Exception == null)
										{
											conflictActifactManager.RemoveArtifact(fileName);
										}
									}
									CreateSyncingConfiguration(fileName, destination);
									synchronizationQueue.SynchronizationFinished(fileName, fileEtag, destination);

				              		return report;
				              	});
		}

		private Task<SynchronizationReport> SynchronizationExceptionReport(string exceptionMessage)
		{
			return new CompletedTask<SynchronizationReport>(new SynchronizationReport()
			                                                	{
			                                                		Exception = new SynchronizationException(exceptionMessage)
			                                                	});
		}

		private IEnumerable<FileHeader> GetFilesToSynchronization(Task<SourceSynchronizationInformation> lastEtagTask, int take)
		{
			var destinationsSynchronizationInformationForSource = lastEtagTask.Result;
			var destinationId = destinationsSynchronizationInformationForSource.DestinationServerInstanceId.ToString();

			IEnumerable<FileHeader> filesToSynchronization = Enumerable.Empty<FileHeader>();

			storage.Batch(
				accessor =>
				filesToSynchronization =
				accessor.GetFilesAfter(destinationsSynchronizationInformationForSource.LastSourceFileEtag, take)
					.Where(x => x.Metadata[SynchronizationConstants.RavenReplicationSource] != destinationId));

			return filesToSynchronization;
		}

		private Task<IEnumerable<SynchronizationConfirmation>> ConfirmPushedFiles(List<string> filesNeedConfirmation, RavenFileSystemClient destinationClient)
		{
			if (filesNeedConfirmation.Count == 0)
			{
				return new CompletedTask<IEnumerable<SynchronizationConfirmation>>(Enumerable.Empty<SynchronizationConfirmation>());
			}
			return destinationClient.Synchronization.ConfirmFilesAsync(filesNeedConfirmation);
		}

		private List<string> GetSyncingConfigurations(string destination)
		{
			IList<SynchronizationDetails> configObjects = null;
			storage.Batch(
				accessor =>
				{
					var configKeys =
						from item in accessor.GetConfigNames()
						where SynchronizationHelper.IsSyncName(item, destination)
						select item;
					configObjects =
						(from item in configKeys
						 select accessor.GetConfigurationValue<SynchronizationDetails>(item)).ToList();
				});

			return configObjects.Select(x => x.FileName).ToList();
		}

		private void CreateSyncingConfiguration(string fileName, string destination)
		{
			storage.Batch(accessor =>
			{
				var name = SynchronizationHelper.SyncNameForFile(fileName, destination);
				accessor.SetConfigurationValue(name, new SynchronizationDetails()
				{
					DestinationUrl = destination,
					FileName = fileName
				});
			});
		}

		private void RemoveSyncingConfiguration(string fileName, string destination)
		{
			storage.Batch(accessor =>
			{
				var name = SynchronizationHelper.SyncNameForFile(fileName, destination);
				accessor.DeleteConfig(name);
			});
		}

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, string fileName, SignatureManifest sourceSignatureManifest, NameValueCollection sourceMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			
			var localFile = StorageStream.Reading(storage, fileName);

			IList<RdcNeed> needList = null;
			using (var signatureRepository = new StorageSignatureRepository(storage, fileName))
			using (var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository))
			{
				needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
			}

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, sourceMetadata, localFile, needList, localFile);
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
			var multipartRequest = new SynchronizationMultipartRequest(destinationServerUrl, localRavenFileSystem.ServerUrl, fileName, sourceMetadata,
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

		private IEnumerable<string> GetSynchronizationDestinations()
		{
			var destinationsConfigExists = false;
			storage.Batch(accessor => destinationsConfigExists = accessor.ConfigExists(SynchronizationConstants.RavenReplicationDestinations));

			if (!destinationsConfigExists)
			{
				return Enumerable.Empty<string>();
			}

			var destionationsConfig = new NameValueCollection();

			storage.Batch(accessor => destionationsConfig = accessor.GetConfig(SynchronizationConstants.RavenReplicationDestinations));

			string[] destinations = destionationsConfig.GetValues("url");

			return destinations;
		}
	}
}