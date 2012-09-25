namespace RavenFS.Infrastructure
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Reactive.Linq;
	using Microsoft.Isam.Esent.Interop;
	using NLog;
	using Search;
	using Storage;
	using Synchronization;
	using Util;

	public class StorageCleanupTask
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;

		private readonly IObservable<long> timer = Observable.Interval(TimeSpan.FromMinutes(10));

		public StorageCleanupTask(TransactionalStorage storage, IndexStorage search)
		{
			this.storage = storage;
			this.search = search;

			InitializeTimer();
		}

		private void InitializeTimer()
		{
			//timer.Subscribe(tick => );
		}

		public void DeleteFile(string fileName)
		{
			var deletingFileName = RavenFileNameHelper.DeletingFileName(fileName);
			var fileExists = true;

			ConcurrencyAwareExecutor.Execute(() =>
			    storage.Batch(accessor =>
			    {
				    FileAndPages fileAndPages;

				    try
				    {
					    fileAndPages = accessor.GetFile(fileName, 0, 0);
				    }
				    catch (FileNotFoundException)
				    {
					    // do nothing if file does not exist
					    fileExists = false;
					    return;
				    }

				    var metadata = new NameValueCollection(fileAndPages.Metadata)
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

					    log.Debug(string.Format("File '{0}' was renamed to '{1}' and marked as deleted",
					                            fileName, deletingFileName));
				    }
				    else
				    {
					    log.Warn("Could not rename a file '{0}' when a delete operation was performed",
					            fileName);
				    }
			    })
			);

			if (fileExists)
			{
				search.Delete(fileName);
				search.Delete(deletingFileName);
			}
		}
	}
}