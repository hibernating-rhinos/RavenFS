namespace RavenFS.Infrastructure
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Reactive.Linq;
	using System.Threading.Tasks;
	using Extensions;
	using Microsoft.Isam.Esent.Interop;
	using NLog;
	using Notifications;
	using Search;
	using Storage;
	using Synchronization;
	using Util;

	public class StorageCleanupTask
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;
		private readonly INotificationPublisher notificationPublisher;
		private readonly ConcurrentDictionary<string, Task> deleteFileTasks = new ConcurrentDictionary<string, Task>();
		private readonly FileLockManager fileLockManager = new FileLockManager();

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(15));

		public StorageCleanupTask(TransactionalStorage storage, IndexStorage search, INotificationPublisher notificationPublisher)
		{
			this.storage = storage;
			this.search = search;
			this.notificationPublisher = notificationPublisher;

			InitializeTimer();
		}

		private void InitializeTimer()
		{
			timer.Subscribe(tick => PerformAsync());
		}

		public void IndicateFileToDelete(string fileName)
		{
			var deletingFileName = RavenFileNameHelper.DeletingFileName(fileName);
			var fileExists = true;

			storage.Batch(accessor =>
			{
				var existingFileHeader = accessor.ReadFile(fileName);

				if(existingFileHeader == null)
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

				var metadata = new NameValueCollection(existingFileHeader.Metadata)
					               {
						               {SynchronizationConstants.RavenDeleteMarker, "true"}
					               };

				var renameSucceeded = false;

				var deleteVersion = 0;

				do
				{
					try
					{
						accessor.RenameFile(fileName, deletingFileName);
						renameSucceeded = true;
					}
					catch (EsentKeyDuplicateException)
					{
						// it means that .deleting file was already existed
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

					var configName = RavenFileNameHelper.DeletingFileConfigNameForFile(deletingFileName);
					accessor.SetConfigurationValue(configName,
					                               new DeleteFile() {OriginalFileName = fileName, CurrentFileName = deletingFileName});

					notificationPublisher.Publish(new ConfigChange() { Name = configName, Action = ConfigChangeAction.Set });
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

		public Task PerformAsync()
		{
			IList<DeleteFile> filesToDelete = null;

			storage.Batch(
				accessor =>
				filesToDelete = accessor.GetConfigsWithPrefix<DeleteFile>(RavenFileNameHelper.DeletingFileConfigPrefix, 0, 10));

			var tasks = new List<Task>();

			foreach (var fileToDelete in filesToDelete)
			{
				var deletingFileName = fileToDelete.CurrentFileName;

				Task existingTask;

				if (deleteFileTasks.TryGetValue(deletingFileName, out existingTask))
					if (!existingTask.IsCompleted)
						continue;

				FileHeader deletedFile = null;
				storage.Batch(accessor => deletedFile = accessor.ReadFile(fileToDelete.OriginalFileName));

				if (deletedFile != null) // if there exists a file already marked as deleted
					if (deletedFile.IsFileBeingUploaded()) // and it's being currently uploaded
						continue; // prevent delete operation because they might have common pages that can be reused by upload

				if(!fileLockManager.TimeoutExceeded(fileToDelete.OriginalFileName, storage))
					continue; // if original file is locked which means that is being synced do not delete it

				log.Debug("Starting to delete file '{0}' from storage", deletingFileName);

				var deleteTask  = TaskEx.Run(
					() => ConcurrencyAwareExecutor.Execute(() => storage.Batch(accessor => accessor.Delete(deletingFileName)))).ContinueWith(
						t =>
						{
							if (t.Exception == null)
							{
								var configName = RavenFileNameHelper.DeletingFileConfigNameForFile(deletingFileName);

								storage.Batch(accessor => accessor.DeleteConfig(configName));
								
								notificationPublisher.Publish(new ConfigChange() { Name = configName, Action = ConfigChangeAction.Delete });
								
								log.Debug("File '{0}' was deleted from storage", deletingFileName);
							}
							else
							{
								log.WarnException(string.Format("Could not delete file '{0}' from storage", deletingFileName), t.Exception);
							}

							deleteFileTasks.TryRemove(deletingFileName, out existingTask);
						});

				deleteFileTasks.AddOrUpdate(deletingFileName, deleteTask, (file, oldTask) => deleteTask);

				tasks.Add(deleteTask);
			}

			return TaskEx.WhenAll(tasks);
		}
	}
}