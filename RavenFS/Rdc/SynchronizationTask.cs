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
	using Storage;
	using Util;
	using Wrapper;

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
			timer.Subscribe(tick => SynchronizeDestinations());
		}

		public IEnumerable<Task<DestinationSyncResult>> SynchronizeDestinations()
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

						var filesToSynchronization = GetFilesToSynchronization(etagTask, 100);

						foreach (var fileHeader in filesToSynchronization)
						{
							synchronizationQueue.EnqueueSynchronization(destinationUrl,
							                                            new RdcWorkItem(fileHeader.Name,
							                                                            localRavenFileSystem.ServerUrl, storage,
							                                                            sigGenerator));
						}

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
															 new RdcWorkItem(confirmation.FileName, localRavenFileSystem.ServerUrl,
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
								Task.Factory.ContinueWhenAll(syncingDestTask.Result.ToArray(), t => new DestinationSyncResult
																						{
																							DestinationServer = destinationUrl,
																							Reports = t.Select(syncingTask => syncingTask.Result)
																						}))
							.Unwrap();
					}).Unwrap();
			}
		}

		public void ProcessWork(SynchronizationWorkItem workItem)
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				synchronizationQueue.EnqueueSynchronization(destination, workItem);
			}

			SynchronizeDestinations();
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

		private IEnumerable<FileHeader> GetFilesToSynchronization(Task<SourceSynchronizationInformation> lastEtagTask, int take)
		{
			var destinationsSynchronizationInformationForSource = lastEtagTask.Result;
			var destinationId = destinationsSynchronizationInformationForSource.DestinationServerInstanceId.ToString();

			IEnumerable<FileHeader> filesToSynchronization = Enumerable.Empty<FileHeader>();

			storage.Batch(
				accessor =>
				filesToSynchronization =
				accessor.GetFilesAfter(destinationsSynchronizationInformationForSource.LastSourceFileEtag, take)
					.Where(x => x.Metadata[SynchronizationConstants.RavenReplicationSource] != destinationId)); // prevent synchronizing to itself

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

			string[] destinations = destionationsConfig.GetValues("url");

			return destinations;
		}
	}
}