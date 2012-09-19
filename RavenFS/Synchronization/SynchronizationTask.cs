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
	using Notifications;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Rdc.Wrapper;
	using SynchronizationAction = Notifications.SynchronizationAction;
	using SynchronizationDirection = Notifications.SynchronizationDirection;
	using SynchronizationUpdate = Notifications.SynchronizationUpdate;

	public class SynchronizationTask
	{
		private const int DefaultLimitOfConcurrentSynchronizations = 5;

		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly SynchronizationQueue synchronizationQueue;
		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;
		private readonly NotificationPublisher publisher;

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(10));

		public SynchronizationTask(TransactionalStorage storage, SigGenerator sigGenerator, NotificationPublisher publisher)
		{
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			this.publisher = publisher;
			synchronizationQueue = new SynchronizationQueue();

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

		public async Task<IEnumerable<DestinationSyncResult>> SynchronizeDestinationsAsync(bool forceSyncingContinuation = true)
		{
			log.Debug("Starting to synchronize destinations");

			var destinationSyncTasks = new List<Task<DestinationSyncResult>>();

			foreach (var destination in GetSynchronizationDestinations())
			{
				log.Debug("Starting to synchronize a destination server {0}", destination);

				var destinationUrl = destination;

				if (!CanSynchronizeTo(destinationUrl))
				{
					log.Debug("Could not synchronize to {0} because no synchronization request was available", destination);
					continue;
				}

				destinationSyncTasks.Add(SynchronizeDestinationAsync(destinationUrl, forceSyncingContinuation));
			}

			return await TaskEx.WhenAll(destinationSyncTasks);
		}

		public async Task<SynchronizationReport> SynchronizeFileToAsync(string fileName, string destinationUrl)
		{
			var destinationClient = new RavenFileSystemClient(destinationUrl);
			NameValueCollection destinationMetadata;

			try
			{
				destinationMetadata = await destinationClient.GetMetadataForAsync(fileName);
			}
			catch (Exception ex)
			{
				log.WarnException("Could not get metadata details for " + fileName + " from " + destinationUrl, ex);
				return SynchronizationUtils.SynchronizationExceptionReport(fileName, ex.ToString());
			}

			var localMetadata = GetLocalMetadata(fileName);

			NoSyncReason reason;
			var work = DetermineSynchronizationWork(fileName, localMetadata, destinationMetadata, out reason);

			if (work == null)
			{
				log.Debug("File '{0}' were not synchronized to {1}. {2}", fileName, destinationUrl, reason.GetDescription());

				return SynchronizationUtils.SynchronizationExceptionReport(fileName, reason.GetDescription());
			}

			return await PerformSynchronizationAsync(destinationClient.ServerUrl, work);
		}

		private async Task<DestinationSyncResult> SynchronizeDestinationAsync(string destinationUrl, bool forceSyncingContinuation)
		{
			try
			{
				var destinationClient = new RavenFileSystemClient(destinationUrl);

				var lastETag = await destinationClient.Synchronization.GetLastSynchronizationFromAsync(storage.Id);

				var filesNeedConfirmation = GetSyncingConfigurations(destinationUrl);

				var confirmations = await ConfirmPushedFiles(filesNeedConfirmation, destinationClient);

				var needSyncingAgain = new List<FileHeader>();

				foreach (var confirmation in confirmations)
				{
					if (confirmation.Status == FileStatus.Safe)
					{
						RemoveSyncingConfiguration(confirmation.FileName, destinationUrl);
						log.Debug("Destination server {0} said that file '{1}' is safe", destinationUrl, confirmation.FileName);
					}
					else
					{
						storage.Batch(accessor =>
						{
							var fileHeader = accessor.ReadFile(confirmation.FileName);
							if (FileIsNotBeingUploaded(fileHeader))
							{
								needSyncingAgain.Add(fileHeader);
							}
						});

						log.Debug(
							"Destination server {0} said that file '{1}' is {2}. File will be added to a synchronization queue again.",
							destinationUrl, confirmation.FileName, confirmation.Status);
					}
				}

				await EnqueueMissingUpdatesAsync(destinationClient, lastETag, needSyncingAgain);

				var reports = await TaskEx.WhenAll(SynchronizePendingFilesAsync(destinationUrl, forceSyncingContinuation));

				var desinationSyncResult = new DestinationSyncResult
					                           {
						                           DestinationServer = destinationUrl
					                           };

				if (reports.Length > 0)
				{
					var successfullSynchronizationsCount = reports.Count(x => x.Exception == null);

					var failedSynchronizationsCount = reports.Count(x => x.Exception != null);

					if (successfullSynchronizationsCount > 0 || failedSynchronizationsCount > 0)
					{
						log.Debug(
							"Synchronization to a destination {0} has completed. {1} file(s) were synchronized successfully, {2} synchonization(s) were failed",
							destinationUrl, successfullSynchronizationsCount, failedSynchronizationsCount);
					}

					desinationSyncResult.Reports = reports;
				}

				return desinationSyncResult;
			}
			catch (Exception ex)
			{
				log.WarnException(string.Format("Failed to perform a synchronization to a destination {0}", destinationUrl), ex);

				return new DestinationSyncResult
							{
								DestinationServer = destinationUrl,
								Exception = ex
							};
			}
		}

		private async Task EnqueueMissingUpdatesAsync(RavenFileSystemClient destinationClient, SourceSynchronizationInformation lastEtag, IEnumerable<FileHeader> needSyncingAgain)
		{
			var destinationUrl = destinationClient.ServerUrl;
			var filesToSynchronization = GetFilesToSynchronization(lastEtag, 100);

			filesToSynchronization.AddRange(needSyncingAgain);

			if (filesToSynchronization.Count == 0)
			{
				return;
			}

			foreach (var fileHeader in filesToSynchronization)
			{
				var file = fileHeader.Name;
				var localMetadata = GetLocalMetadata(file);

				NameValueCollection destinationMetadata;

				try
				{
					destinationMetadata = await destinationClient.GetMetadataForAsync(file);
				}
				catch (Exception ex)
				{
					log.WarnException(
						string.Format(
							"Could not retrieve a metadata of a file '{0}' from {1} in order to determine needed synchronization type", file,
							destinationUrl), ex);

					continue;
				}

				NoSyncReason reason;
				var work = DetermineSynchronizationWork(file, localMetadata, destinationMetadata, out reason);

				if (work == null)
				{
					log.Debug("File '{0}' were not synchronized to {1}. {2}", file, destinationUrl, reason.GetDescription());

					if (reason == NoSyncReason.ContainedInDestinationHistory)
					{
						var etag = localMetadata.Value<Guid>("ETag");
						destinationClient.Synchronization.IncrementLastETagAsync(storage.Id, etag);
						RemoveSyncingConfiguration(file, destinationClient.ServerUrl);
					}
					
					continue;
				}

				synchronizationQueue.EnqueueSynchronization(destinationUrl, work);
			}
		}

		private SynchronizationWorkItem DetermineSynchronizationWork(string file, NameValueCollection localMetadata, NameValueCollection destinationMetadata, out NoSyncReason reason)
		{
			reason = NoSyncReason.Unknown;

			if (localMetadata == null)
			{
				reason = NoSyncReason.SourceFileNotExist;
				return null;
			}

			if (destinationMetadata != null &&
					destinationMetadata[SynchronizationConstants.RavenSynchronizationConflict] != null
					&& destinationMetadata[SynchronizationConstants.RavenSynchronizationConflictResolution] == null)
			{
				reason = NoSyncReason.DestinationFileConflicted;
				return null;
			}

			if (localMetadata[SynchronizationConstants.RavenSynchronizationConflict] != null)
			{
				reason = NoSyncReason.SourceFileConflicted;
				return null;
			}

			if (localMetadata[SynchronizationConstants.RavenDeleteMarker] != null)
			{
				var rename = localMetadata[SynchronizationConstants.RavenRenameFile];

				if (rename != null)
				{
					return new RenameWorkItem(file, rename, storage);
				}
				return new DeleteWorkItem(file, storage);
			}

			if (destinationMetadata != null && Historian.IsDirectChildOfCurrent(localMetadata, destinationMetadata))
			{
				reason = NoSyncReason.ContainedInDestinationHistory;
				return null;
			}

			if (destinationMetadata != null && localMetadata["Content-MD5"] == destinationMetadata["Content-MD5"]) // file exists on dest and has the same content
			{
				// check metadata to detect if any synchronization is needed
				if (localMetadata.AllKeys.Except(new[] { "ETag", "Last-Modified" }).Any(key => !destinationMetadata.AllKeys.Contains(key) || localMetadata[key] != destinationMetadata[key]))
				{
					return new MetadataUpdateWorkItem(file, destinationMetadata, storage);
				}

				reason = NoSyncReason.SameContentAndMetadata;

				return null; // the same content and metadata - no need to synchronize
			}
			return new ContentUpdateWorkItem(file, storage, sigGenerator);
		}

		private IEnumerable<Task<SynchronizationReport>> SynchronizePendingFilesAsync(string destinationUrl, bool forceSyncingContinuation)
		{
			for (var i = 0; i < AvailableSynchronizationRequestsTo(destinationUrl); i++)
			{
				SynchronizationWorkItem work;
				if (synchronizationQueue.TryDequeuePendingSynchronization(destinationUrl, out work))
				{
					if (synchronizationQueue.IsDifferentWorkForTheSameFileBeingPerformed(work, destinationUrl))
					{
						log.Debug("There was an alredy being performed synchronization of a file '{0}' to {1}", work.FileName,
								  destinationUrl);
						synchronizationQueue.EnqueueSynchronization(destinationUrl, work); // add it again at the end of the queue
					}
					else
					{
						var workTask = PerformSynchronizationAsync(destinationUrl, work);

						if (forceSyncingContinuation)
						{
							workTask.ContinueWith(t => SynchronizePendingFilesAsync(destinationUrl, true).ToArray());
						}
						yield return workTask;
					}
				}
				else
				{
					break;
				}
			}
		}

		private async Task<SynchronizationReport> PerformSynchronizationAsync(string destinationUrl, SynchronizationWorkItem work)
		{
			log.Debug("Starting to perform {0} for a file '{1}' and a destination server {2}", work.GetType().Name, work.FileName,
			          destinationUrl);

			if (!CanSynchronizeTo(destinationUrl))
			{
				log.Debug("The limit of active synchronizations to {0} server has been achieved. Cannot process a file '{1}'.", destinationUrl, work.FileName);

				synchronizationQueue.EnqueueSynchronization(destinationUrl, work);

				return SynchronizationUtils.SynchronizationExceptionReport(work.FileName,
				                                                           string.Format(
					                                                           "The limit of active synchronizations to {0} server has been achieved. Cannot process a file '{1}'.",
					                                                           destinationUrl, work.FileName));
			}

			var fileName = work.FileName;
			synchronizationQueue.SynchronizationStarted(work, destinationUrl);
			publisher.Publish(new SynchronizationUpdate
			                  	{
			                  		FileName = work.FileName,
									DestinationServer = destinationUrl,
									SourceServerId = storage.Id,
									Type = work.SynchronizationType,
									Action = SynchronizationAction.Start,
									SynchronizationDirection = SynchronizationDirection.Outgoing
			                  	});

			SynchronizationReport report;
			
			try
			{
				report = await work.PerformAsync(destinationUrl);
			}
			catch (Exception ex)
			{
				report = new SynchronizationReport
				{
					FileName = work.FileName,
					Exception = ex,
					Type = work.SynchronizationType
				};
			}

			bool synchronizationCancelled = false;

			if (report.Exception == null)
			{
				var moreDetails = string.Empty;

				if (work.SynchronizationType == SynchronizationType.ContentUpdate)
				{
					moreDetails = string.Format(". {0} bytes were transfered and {1} bytes copied. Need list length was {2}",
					                            report.BytesTransfered, report.BytesCopied, report.NeedListLength);
				}

				log.Debug("{0} to {1} has finished successfully{2}", work.ToString(), destinationUrl, moreDetails);
			}
			else
			{
				if (work.IsCancelled || report.Exception is TaskCanceledException)
				{
					synchronizationCancelled = true;
					log.DebugException(string.Format("{0} to {1} was cancelled", work, destinationUrl), report.Exception);
				}
				else
				{
					log.WarnException(string.Format("{0} to {1} has finished with the exception", work, destinationUrl),
					                  report.Exception);
				}
			}

			Queue.SynchronizationFinished(work, destinationUrl);
			
			if(!synchronizationCancelled)
			{
				CreateSyncingConfiguration(fileName, work.FileETag, destinationUrl, work.SynchronizationType);
			}

			publisher.Publish(new SynchronizationUpdate
			{
				FileName = work.FileName,
				DestinationServer = destinationUrl,
				SourceServerId = storage.Id,
				Type = work.SynchronizationType,
				Action = SynchronizationAction.Finish,
				SynchronizationDirection = SynchronizationDirection.Outgoing
			});

			return report;
		}

		private List<FileHeader> GetFilesToSynchronization(SourceSynchronizationInformation destinationsSynchronizationInformationForSource, int take)
		{
			var filesToSynchronization = new List<FileHeader>();

			log.Debug("Getting files to synchronize with ETag greater than {0}",
			          destinationsSynchronizationInformationForSource.LastSourceFileEtag);

			try
			{
				var destinationId = destinationsSynchronizationInformationForSource.DestinationServerInstanceId.ToString();

				IList<FileHeader> candidatesToSynchronization = null;

				storage.Batch(
					accessor =>
					candidatesToSynchronization =
					accessor.GetFilesAfter(destinationsSynchronizationInformationForSource.LastSourceFileEtag, take)
						.Where(x => x.Metadata[SynchronizationConstants.RavenSynchronizationSource] != destinationId // prevent synchronization back to source
									&& FileIsNotBeingUploaded(x)).ToList());
				
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

			log.Debug("There were {0} file(s) that needed synchronization ({1}) because of greater ETag value", filesToSynchronization.Count,
			          string.Join(",",
			                      filesToSynchronization.Select(
			                      	x => string.Format("{0} [ETag {1}]", x.Name, x.Metadata.Value<Guid>("ETag")))));

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
							configObjects = accessor.GetConfigsStartWithPrefix<SynchronizationDetails>(SynchronizationHelper.SyncNamePrefix, 0, 100);
						});
			}
			catch (Exception e)
			{
				log.WarnException(string.Format("Could not get syncing configurations for a destination {0}", destination), e);
			}

			return configObjects.Select(x => x.FileName).ToList();
		}

		private void CreateSyncingConfiguration(string fileName, Guid etag, string destination, SynchronizationType synchronizationType)
		{
			try
			{
				var name = SynchronizationHelper.SyncNameForFile(fileName, destination);
				storage.Batch(accessor => accessor.SetConfigurationValue(name, new SynchronizationDetails
				                                                               	{
				                                                               		DestinationUrl = destination,
				                                                               		FileName = fileName,
																					FileETag = etag,
																					Type = synchronizationType
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
				log.Debug("Configuration " + SynchronizationConstants.RavenSynchronizationDestinations  + " does not exist");
				return Enumerable.Empty<string>();
			}

			var destionationsConfig = new NameValueCollection();

			storage.Batch(accessor => destionationsConfig = accessor.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations));

			var destinations = destionationsConfig.GetValues("url");

			if (destinations == null)
			{
				log.Warn("Invalid " + SynchronizationConstants.RavenSynchronizationDestinations + " configuration");
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
				log.Warn("Configuration " + SynchronizationConstants.RavenSynchronizationDestinations + " does not contain any destination");
			}

			return destinations;
		}

		private bool CanSynchronizeTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() > synchronizationQueue.NumberOfActiveSynchronizationTasksFor(destination);
		}

		private int AvailableSynchronizationRequestsTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() - synchronizationQueue.NumberOfActiveSynchronizationTasksFor(destination);
		}

		private int LimitOfConcurrentSynchronizations()
		{
			bool limit = false;
			int configuredLimit = 0;

			storage.Batch(
				accessor =>
				limit = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenSynchronizationLimit, out configuredLimit));

			return limit ? configuredLimit : DefaultLimitOfConcurrentSynchronizations;
		}

		private static bool FileIsNotBeingUploaded(FileHeader header)
		{			// do not synchronize files that are being uploaded
			return header.TotalSize != null && header.TotalSize == header.UploadedSize
				// even if the file is uploaded make sure file has Content-MD5
				// it's necessary to determine synchronization type and ensures right ETag
				   && (header.Metadata[SynchronizationConstants.RavenDeleteMarker] != null || header.Metadata["Content-MD5"] != null);
		}

		public void Cancel(string fileName)
		{
			log.Debug("Cancellation of active synchronizations of a file '{0}'", fileName);
			Queue.CancelActiveSynchronizations(fileName);
		}
	}
}