namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Reactive.Linq;
	using System.Threading.Tasks;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Rdc.Wrapper;

	public class SynchronizationTask
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly SynchronizationQueue synchronizationQueue;
		private readonly RavenFileSystem localRavenFileSystem;
		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(10));

		public SynchronizationTask(RavenFileSystem localRavenFileSystem, TransactionalStorage storage, SigGenerator sigGenerator)
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
			log.Debug("Starting to synchronize destinations");

			var task = new Task<Task<DestinationSyncResult>[]>(() => SynchronizeDestinationsInternal().ToArray());
			task.Start();
			return task;
		}

		private IEnumerable<Task<DestinationSyncResult>> SynchronizeDestinationsInternal()
		{
			foreach (var destination in GetSynchronizationDestinations())
			{
				log.Debug("Starting to synchronize a destination server {0}", destination);

				string destinationUrl = destination;

				if (!synchronizationQueue.CanSynchronizeTo(destinationUrl))
				{
					log.Debug("Could not synchronize to {0} because no synchronization request was available", destination);
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
															log.Debug("Destination server {0} said that file '{1}' is safe", destinationUrl, confirmation.FileName);
														}
														else
														{
															synchronizationQueue.EnqueueSynchronization(destinationUrl,
																 new ContentUpdateWorkItem(confirmation.FileName, localRavenFileSystem.ServerUrl,
																							 storage, sigGenerator));
															log.Debug(
																"Destination server {0} said that file '{1}' is {2}. File was added to a synchronization queue again.",
																destinationUrl, confirmation.FileName, confirmation.Status);

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
					}).Unwrap()
					.ContinueWith(t =>
					              	{
					              		if (t.Exception != null)
					              		{
					              			var exception = t.Exception.ExtractSingleInnerException();

											log.WarnException(string.Format("Failed to perform a synchronization to a destination {0}", destinationUrl), exception);

											return new DestinationSyncResult
											{
												DestinationServer = destinationUrl,
												Exception = exception
											};
					              		}

										log.Debug("Synchronization to a destination {0} was completed. {1} files were synchronized", destinationUrl, t.Result.Reports != null ? t.Result.Reports.Count() : 0);

					              		return t.Result;
					              	});
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
						if (t.Exception != null)
						{
							log.WarnException(
								string.Format(
									"Could not retrieve a metadata of a file '{0}' from {1} in order to determine needed synchronization type", file,
									destinationUrl), t.Exception.ExtractSingleInnerException());
							return;
						}

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
			log.Debug("Starting to perform {0} for a file '{1}' and a destination server {2}", work.GetType().Name, work.FileName,
			          destinationUrl);

			if (!Queue.CanSynchronizeTo(destinationUrl))
			{
				log.Debug("The limit of active synchronizations to {0} server has been achieved.", destinationUrl);

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
				              		Queue.SynchronizationFinished(fileName, fileETag, destinationUrl);

				              		if (t.Exception != null)
				              		{
				              			log.WarnException(
				              				string.Format(
				              					"An exception was thrown during work {0} performing for file '{1}' and a destination {2}",
				              					work.GetType().Name, fileName, destinationUrl), t.Exception.ExtractSingleInnerException());
				              		}
				              		else if (t.Exception == null && t.Result.Exception == null)
				              		{
				              			CreateSyncingConfiguration(fileName, destinationUrl);
				              		}

				              		return t.Result;
				              	});
		}

		private IEnumerable<FileHeader> GetFilesToSynchronization(SourceSynchronizationInformation destinationsSynchronizationInformationForSource, int take)
		{
			var filesToSynchronization = new List<FileHeader>();

			try
			{
				var destinationId = destinationsSynchronizationInformationForSource.DestinationServerInstanceId.ToString();

				var candidatesToSynchronization = Enumerable.Empty<FileHeader>();

				storage.Batch(
					accessor =>
					candidatesToSynchronization =
					accessor.GetFilesAfter(destinationsSynchronizationInformationForSource.LastSourceFileEtag, take)
						.Where(x => x.Metadata[SynchronizationConstants.RavenSynchronizationSource] != destinationId)); // prevent synchronization back to source

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
			}
			catch (Exception e)
			{
				log.WarnException(string.Format("Could not get files to synchronize after: " + destinationsSynchronizationInformationForSource.LastSourceFileEtag), e);
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
			IList<SynchronizationDetails> configObjects = new List<SynchronizationDetails>();

			try
			{
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
			}
			catch (Exception e)
			{
				log.WarnException(string.Format("Could not get syncing configurations for a destination {0}", destination), e);
			}

			return configObjects.Select(x => x.FileName).ToList();
		}

		private void CreateSyncingConfiguration(string fileName, string destination)
		{
			try
			{
				var name = SynchronizationHelper.SyncNameForFile(fileName, destination);
				storage.Batch(accessor => accessor.SetConfigurationValue(name, new SynchronizationDetails
				                                                               	{
				                                                               		DestinationUrl = destination,
				                                                               		FileName = fileName
				                                                               	}));
			}
			catch (Exception e)
			{
				log.WarnException(
					string.Format("Could not create syncing configurations for a file {0} and destination {1}", fileName, destination), e);
			}
		}

		private void RemoveSyncingConfiguration(string fileName, string destination)
		{
			try
			{
				var name = SynchronizationHelper.SyncNameForFile(fileName, destination);
				storage.Batch(accessor => accessor.DeleteConfig(name));
			}
			catch (Exception e)
			{
				log.WarnException(
					string.Format("Could not remove syncing configurations for a file {0} and a destination {1}", fileName, destination), e);
			}
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
			storage.Batch(accessor => destinationsConfigExists = accessor.ConfigExists(SynchronizationConstants.RavenSynchronizationDestinations));

			if (!destinationsConfigExists)
			{
				log.Debug("Configuration Raven/Synchronization/Destinations does not exist");
				return Enumerable.Empty<string>();
			}

			var destionationsConfig = new NameValueCollection();

			storage.Batch(accessor => destionationsConfig = accessor.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations));

			var destinations = destionationsConfig.GetValues("url");

			if (destinations == null)
			{
				log.Warn("Invalid Raven/Synchronization/Destinations configuration");
				return Enumerable.Empty<string>();
			}

			for (int i = 0; i < destinations.Length; i++)
			{
				if (destinations[i].EndsWith("/"))
				{
					destinations[i] = destinations[i].Substring(0, destinations[i].Length - 1);
				}
			}

			if (destinations.Length == 0)
			{
				log.Warn("Configuration Raven/Synchronization/Destinations does not contain any destination");
			}

			return destinations;
		}
	}
}