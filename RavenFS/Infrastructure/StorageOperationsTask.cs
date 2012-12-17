namespace RavenFS.Infrastructure
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Linq;
	using System.Reactive.Linq;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Microsoft.Isam.Esent.Interop;
	using NLog;
	using Notifications;
	using Search;
	using Storage;
	using Synchronization;
	using Util;

	public class StorageOperationsTask
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;
		private readonly INotificationPublisher notificationPublisher;
		private readonly ConcurrentDictionary<string, Task> deleteFileTasks = new ConcurrentDictionary<string, Task>();
		private readonly ConcurrentDictionary<string, Task> renameFileTasks = new ConcurrentDictionary<string, Task>();
		private readonly ConcurrentDictionary<string, FileHeader> uploadingFiles = new ConcurrentDictionary<string, FileHeader>();
		private readonly FileLockManager fileLockManager = new FileLockManager();

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(15));

		public StorageOperationsTask(TransactionalStorage storage, IndexStorage search, INotificationPublisher notificationPublisher)
		{
			this.storage = storage;
			this.search = search;
			this.notificationPublisher = notificationPublisher;

			InitializeTimer();
		}

		private void InitializeTimer()
		{
			timer.Subscribe(tick =>
			{
				ResumeFileRenamingAsync();
				CleanupDeletedFilesAsync();
			});
		}

		public void RenameFile(RenameFileOperation operation)
		{
			var configName = RavenFileNameHelper.RenameOperationConfigNameForFile(operation.Name);
			notificationPublisher.Publish(new FileChange { File = FilePathTools.Cannoicalise(operation.Name), Action = FileChangeAction.Renaming });

			storage.Batch(accessor =>
			{
				var previousRenameTombstone = accessor.ReadFile(operation.Rename);

				if (previousRenameTombstone != null && previousRenameTombstone.Metadata[SynchronizationConstants.RavenDeleteMarker] != null)
				{
					// if there is a tombstone delete it
					accessor.Delete(previousRenameTombstone.Name);
				}

				accessor.RenameFile(operation.Name, operation.Rename, true);
				accessor.UpdateFileMetadata(operation.Rename, operation.MetadataAfterOperation);

				// copy renaming file metadata and set special markers
				var tombstoneMetadata = new NameValueCollection(operation.MetadataAfterOperation).WithRenameMarkers(operation.Rename);

				accessor.PutFile(operation.Name, 0, tombstoneMetadata, true); // put rename tombstone

				accessor.DeleteConfig(configName);

				search.Delete(operation.Name);
				search.Index(operation.Rename, operation.MetadataAfterOperation);
			});

			notificationPublisher.Publish(new ConfigChange { Name = configName, Action = ConfigChangeAction.Set });
			notificationPublisher.Publish(new FileChange { File = FilePathTools.Cannoicalise(operation.Rename), Action = FileChangeAction.Renamed });
		}

		public void IndicateFileToDelete(string fileName)
		{
			var deletingFileName = RavenFileNameHelper.DeletingFileName(fileName);
			var fileExists = true;

			storage.Batch(accessor =>
			{
				var existingFileHeader = accessor.ReadFile(fileName);

				if (existingFileHeader == null)
				{
					// do nothing if file does not exist
					fileExists = false;
					return;
				}

				if (existingFileHeader.Metadata[SynchronizationConstants.RavenDeleteMarker] != null)
				{
					// if it is a tombstone drop it
					accessor.Delete(fileName);
					fileExists = false;
					return;
				}

				var metadata = new NameValueCollection(existingFileHeader.Metadata).WithDeleteMarker();

				var renameSucceeded = false;

				var deleteVersion = 0;

				do
				{
					try
					{
						accessor.RenameFile(fileName, deletingFileName);
						renameSucceeded = true;
					}
					catch (EsentKeyDuplicateException) // it means that .deleting file was already existed
					{
						var deletingFileHeader = accessor.ReadFile(deletingFileName);

						if (deletingFileHeader != null && deletingFileHeader.Equals(existingFileHeader))
						{
							fileExists = false; // the same file already marked as deleted no need to do it again
							return;
						}

						// we need to use different name to do a file rename
						deleteVersion++;
						deletingFileName = RavenFileNameHelper.DeletingFileName(fileName, deleteVersion);
					}
				} while (!renameSucceeded && deleteVersion < 128);

				if (renameSucceeded)
				{
					accessor.UpdateFileMetadata(deletingFileName, metadata);
					accessor.DecrementFileCount();

					log.Debug(string.Format("File '{0}' was renamed to '{1}' and marked as deleted",
											fileName, deletingFileName));

					var configName = RavenFileNameHelper.DeleteOperationConfigNameForFile(deletingFileName);
					accessor.SetConfig(configName,
					                   new DeleteFileOperation {OriginalFileName = fileName, CurrentFileName = deletingFileName}.
						                   AsConfig());

					notificationPublisher.Publish(new ConfigChange { Name = configName, Action = ConfigChangeAction.Set });
				}
				else
				{
					log.Warn("Could not rename a file '{0}' when a delete operation was performed",
							 fileName);
				}
			});

			if (fileExists)
			{
				search.Delete(fileName);
				search.Delete(deletingFileName);
			}
		}

		public Task CleanupDeletedFilesAsync()
		{
			IList<DeleteFileOperation> filesToDelete = null;

			storage.Batch(
				accessor =>
				filesToDelete =
				accessor.GetConfigsStartWithPrefix(RavenFileNameHelper.DeleteOperationConfigPrefix, 0, 10).Select(
					config => config.AsObject<DeleteFileOperation>()).ToList());

			var tasks = new List<Task>();

			foreach (var fileToDelete in filesToDelete)
			{
				var deletingFileName = fileToDelete.CurrentFileName;

				if (IsDeleteInProgress(deletingFileName))
					continue;

				if (IsUploadInProgress(fileToDelete.OriginalFileName))
					continue;

				if (IsSynchronizationInProgress(fileToDelete.OriginalFileName))
					continue;

				if (fileToDelete.OriginalFileName.EndsWith(RavenFileNameHelper.DownloadingFileSuffix)) // if it's .downloading file
				{
					if (IsSynchronizationInProgress(SynchronizedFileName(fileToDelete.OriginalFileName))) // and file is being synced
						continue;
				}

				log.Debug("Starting to delete file '{0}' from storage", deletingFileName);

				var deleteTask = TaskEx.Run(
					() => ConcurrencyAwareExecutor.Execute(() => storage.Batch(accessor => accessor.Delete(deletingFileName)), retries: 1)).ContinueWith(
						t =>
						{
							if (t.Exception == null)
							{
								var configName = RavenFileNameHelper.DeleteOperationConfigNameForFile(deletingFileName);

								storage.Batch(accessor => accessor.DeleteConfig(configName));

								notificationPublisher.Publish(new ConfigChange { Name = configName, Action = ConfigChangeAction.Delete });

								log.Debug("File '{0}' was deleted from storage", deletingFileName);
							}
							else
							{
								log.WarnException(string.Format("Could not delete file '{0}' from storage", deletingFileName), t.Exception);
							}
						});

				deleteFileTasks.AddOrUpdate(deletingFileName, deleteTask, (file, oldTask) => deleteTask);

				tasks.Add(deleteTask);
			}

			return TaskEx.WhenAll(tasks);
		}

		public Task ResumeFileRenamingAsync()
		{
			IList<RenameFileOperation> filesToRename = null;

			storage.Batch(
				accessor =>
				filesToRename =
				accessor.GetConfigsStartWithPrefix(RavenFileNameHelper.RenameOperationConfigPrefix, 0, 10).Select(
					config => config.AsObject<RenameFileOperation>()).ToList());
				
			var tasks = new List<Task>();

			foreach (var item in filesToRename)
			{
				var renameOperation = item;

				if (IsRenameInProgress(renameOperation.Name))
					continue;

				log.Debug("Starting to resume a rename operation of a file '{0}' to '{1}'", renameOperation.Name, renameOperation.Rename);

				var renameTask = TaskEx.Run(
					() => ConcurrencyAwareExecutor.Execute(() => RenameFile(renameOperation), retries: 1)).ContinueWith(
						t =>
						{
							if (t.Exception == null)
							{
								log.Debug("File '{0}' was renamed to '{1}'", renameOperation.Name, renameOperation.Rename);
							}
							else
							{
								log.WarnException(string.Format("Could not rename file '{0}' to '{1}'", renameOperation.Name, renameOperation.Rename), t.Exception);
							}
						});

				renameFileTasks.AddOrUpdate(renameOperation.Name, renameTask, (file, oldTask) => renameTask);

				tasks.Add(renameTask);
			}

			return TaskEx.WhenAll(tasks);
		}

		private static string SynchronizedFileName(string originalFileName)
		{
			return originalFileName.Substring(0,
											  originalFileName.IndexOf(RavenFileNameHelper.DownloadingFileSuffix,
																	   StringComparison.InvariantCulture));
		}

		private bool IsSynchronizationInProgress(string originalFileName)
		{
			if (!fileLockManager.TimeoutExceeded(originalFileName, storage))
				return true;
			return false;
		}

		private bool IsUploadInProgress(string originalFileName)
		{
			FileHeader deletedFile = null;
			storage.Batch(accessor => deletedFile = accessor.ReadFile(originalFileName));

			if (deletedFile != null) // if there exists a file already marked as deleted
			{
				if (deletedFile.IsFileBeingUploadedOrUploadHasBeenBroken()) // and might be uploading at the momemnt
				{
					if (!uploadingFiles.ContainsKey(deletedFile.Name))
					{
						uploadingFiles.TryAdd(deletedFile.Name, deletedFile);
						return true; // first attempt to delete a file, prevent this time
					}
					var uploadingFile = uploadingFiles[deletedFile.Name];
					if (uploadingFile != null && uploadingFile.UploadedSize != deletedFile.UploadedSize)
					{
						return true; // if uploaded size changed it means that file is being uploading
					}
					FileHeader header;
					uploadingFiles.TryRemove(deletedFile.Name, out header);
				}
			}
			return false;
		}

		private bool IsDeleteInProgress(string deletingFileName)
		{
			Task existingTask;

			if (deleteFileTasks.TryGetValue(deletingFileName, out existingTask))
			{
				if (!existingTask.IsCompleted)
				{
					return true;
				}

				deleteFileTasks.TryRemove(deletingFileName, out existingTask);
			}
			return false;
		}

		private bool IsRenameInProgress(string fileName)
		{
			Task existingTask;

			if (renameFileTasks.TryGetValue(fileName, out existingTask))
			{
				if (!existingTask.IsCompleted)
				{
					return true;
				}

				renameFileTasks.TryRemove(fileName, out existingTask);
			}
			return false;
		}
	}
}