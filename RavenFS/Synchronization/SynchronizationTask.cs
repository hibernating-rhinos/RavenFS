namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Reactive.Linq;
	using System.Threading.Tasks;
	using Conflictuality;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Rdc.Wrapper;

	public class SynchronizationTask
	{
		private readonly SynchronizationQueue synchronizationQueue;
		private readonly RavenFileSystem localRavenFileSystem;
		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(10));

		public SynchronizationTask(RavenFileSystem localRavenFileSystem, TransactionalStorage storage, SigGenerator sigGenerator, ConflictActifactManager conflictActifactManager, ConflictDetector conflictDetector, ConflictResolver conflictResolver)
		{
			this.localRavenFileSystem = localRavenFileSystem;
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			synchronizationQueue = new SynchronizationQueue(storage);

			InitializeTimer();
		}

		public SynchronizationQueue Queue
		{
			get { return synchronizationQueue; }
		}

		private void InitializeTimer()
		{
			timer.Subscribe(tick => SynchronizeDestinationsAsync());
		}

		public Task<Task<DestinationSyncResult>[]> SynchronizeDestinationsAsync()
		{
			var task = new Task<Task<DestinationSyncResult>[]>(() => SynchronizeDestinationsInternal().ToArray());
			task.Start();
			return task;
		}

		private IEnumerable<Task<DestinationSyncResult>> SynchronizeDestinationsInternal()
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				string destinationUrl = destination;

				if (!synchronizationQueue.CanSynchronizeTo(destinationUrl))
				{
					continue;
				}

				var destinationClient = new RavenFileSystemClient(destinationUrl);

				yield return destinationClient.Synchronization.GetLastSynchronizationFromAsync(localRavenFileSystem.ServerUrl)
					.ContinueWith(etagTask =>
					{
						etagTask.AssertNotFaulted();

						return EnqueueMissingUpdates(etagTask.Result, destinationClient)
							.ContinueWith(enqueueTask =>
						{
							var filesNeedConfirmation = GetSyncingConfigurations(destinationUrl);

							return ConfirmPushedFiles(filesNeedConfirmation, destinationClient)
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
															synchronizationQueue.EnqueueSynchronization(destinationUrl,
																 new ContentUpdateWorkItem(confirmation.FileName, localRavenFileSystem.ServerUrl,
																							 storage, sigGenerator));
														}
													}
												})
								.ContinueWith(t =>
												{
													t.AssertNotFaulted();
													return SynchronizePendingFiles(destinationUrl);
												})
								.ContinueWith(
									syncingDestTask =>
										{
											var tasks = syncingDestTask.Result.ToArray();

											if(tasks.Length > 0)
											{
												return Task.Factory.ContinueWhenAll(tasks, t => new DestinationSyncResult
																					{
																						DestinationServer = destinationUrl,
																						Reports = t.Select(syncingTask => syncingTask.Result)
																					});
											}

											return new CompletedTask<DestinationSyncResult>(new DestinationSyncResult
											{
												DestinationServer = destinationUrl
											});
										})
								.Unwrap();
						}).Unwrap();
					}).Unwrap();
			}
		}

		private Task EnqueueMissingUpdates(SourceSynchronizationInformation lastEtag, RavenFileSystemClient destinationClient)
		{
			var tcs = new TaskCompletionSource<object>();

			string destinationUrl = destinationClient.ServerUrl;
			var filesToSynchronization = GetFilesToSynchronization(lastEtag, 100).ToArray();

			if (filesToSynchronization.Length == 0)
			{
				tcs.SetResult(null);
				return tcs.Task;
			}

			var gettingMetadataTasks = new List<Task>();

			for (int i = 0; i < filesToSynchronization.Length; i++)
			{
				var file = filesToSynchronization[i].Name;

				var localMetadata = GetLocalMetadata(file);

				var task = destinationClient.GetMetadataForAsync(file)
					.ContinueWith(t =>
					{
						t.AssertNotFaulted();

						if (localMetadata[SynchronizationConstants.RavenDeleteMarker] != null)
						{
							var rename = localMetadata[SynchronizationConstants.RavenRenameFile];

							if (rename != null)
							{
								synchronizationQueue.EnqueueSynchronization(destinationUrl,
																			new RenameWorkItem(file, rename, localRavenFileSystem.ServerUrl, storage));
							}
							else
							{
								synchronizationQueue.EnqueueSynchronization(destinationUrl,
								                                            new DeleteWorkItem(file, localRavenFileSystem.ServerUrl, storage));
							}
						}
						else if (t.Result != null && localMetadata["Content-MD5"] == t.Result["Content-MD5"]) // file exists on dest and has the same content
						{
							synchronizationQueue.EnqueueSynchronization(destinationUrl,
																		new MetadataUpdateWorkItem(file, localMetadata,
																								   localRavenFileSystem.ServerUrl));
						}
						else
						{
							synchronizationQueue.EnqueueSynchronization(destinationUrl,
																		new ContentUpdateWorkItem(file,
																								  localRavenFileSystem.ServerUrl,
																								  storage, sigGenerator));
						}
					});

				gettingMetadataTasks.Add(task);
			}

			Task.Factory.ContinueWhenAll(gettingMetadataTasks.ToArray(), x => tcs.SetResult(null));

			return tcs.Task;
		}

		private IEnumerable<Task<SynchronizationReport>> SynchronizePendingFiles(string destinationUrl)
		{
			for (var i = 0; i < synchronizationQueue.AvailableSynchronizationRequestsTo(destinationUrl); i++)
			{
				SynchronizationWorkItem work;
				if (synchronizationQueue.TryDequeuePendingSynchronization(destinationUrl, out work))
				{
					yield return PerformSynchronization(destinationUrl, work);
				}
				else
				{
					break;
				}
			}
		}

		public Task<SynchronizationReport> PerformSynchronization(string destinationUrl, SynchronizationWorkItem work)
		{
			if (!Queue.CanSynchronizeTo(destinationUrl))
			{
				return
					SynchronizationUtils.SynchronizationExceptionReport(
						string.Format("The limit of active synchronizations to {0} server has been achieved.",
						              destinationUrl));
			}

			var fileName = work.FileName;
			var fileETag = GetLocalMetadata(fileName).Value<Guid>("ETag");
			synchronizationQueue.SynchronizationStarted(fileName, fileETag, destinationUrl);
			return work.Perform(destinationUrl)
				.ContinueWith(t =>
				              	{
				              		CreateSyncingConfiguration(fileName, destinationUrl);
				              		Queue.SynchronizationFinished(fileName, fileETag, destinationUrl);

				              		return t;
				              	}).Unwrap();
		}

		private IEnumerable<FileHeader> GetFilesToSynchronization(SourceSynchronizationInformation lastEtag, int take)
		{
			var destinationsSynchronizationInformationForSource = lastEtag;
			var destinationId = destinationsSynchronizationInformationForSource.DestinationServerInstanceId.ToString();

			var candidatesToSynchronization = Enumerable.Empty<FileHeader>();

			storage.Batch(
				accessor =>
				candidatesToSynchronization =
				accessor.GetFilesAfter(destinationsSynchronizationInformationForSource.LastSourceFileEtag, take)
					.Where(x => x.Metadata[SynchronizationConstants.RavenReplicationSource] != destinationId)); // prevent synchronization back to source

			var filesToSynchronization = new List<FileHeader>();

			foreach (var file in candidatesToSynchronization)
			{
				var fileName = file.Name;

				if (!candidatesToSynchronization.Any(
						x =>
						x.Metadata[SynchronizationConstants.RavenDeleteMarker] != null &&
						x.Metadata[SynchronizationConstants.RavenRenameFile] == fileName)) // do not synchronize entire file after renaming, process only a tombstone file
				{
					filesToSynchronization.Add(file);
				}
			}

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

			var destinations = destionationsConfig.GetValues("url");

			for (int i = 0; i < destinations.Length; i++)
			{
				if (destinations[i].EndsWith("/"))
				{
					destinations[i] = destinations[i].Substring(0, destinations[i].Length - 1);
				}
			}

			return destinations;
		}
	}
}