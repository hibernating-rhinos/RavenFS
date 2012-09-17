using System.Collections.Specialized;
using RavenFS.Search;

namespace RavenFS.Synchronization.Conflictuality
{
	using System;
	using Client;
	using RavenFS.Extensions;
	using RavenFS.Storage;
	using RavenFS.Util;

	public class ConflictArtifactManager
	{
		private readonly TransactionalStorage storage;
	    private readonly IndexStorage index;

	    public ConflictArtifactManager(TransactionalStorage storage, IndexStorage index)
		{
		    this.storage = storage;
		    this.index = index;
		}

	    public void Create(string fileName, ConflictItem conflict)
		{
	        NameValueCollection metadata = null;

	        storage.Batch(
				accessor =>
				{
					metadata = accessor.GetFile(fileName, 0, 0).Metadata;
					accessor.SetConfigurationValue(
						SynchronizationHelper.ConflictConfigNameForFile(fileName), conflict);
					metadata[SynchronizationConstants.RavenSynchronizationConflict] = "True";
					accessor.UpdateFileMetadata(fileName, metadata);
				});

            if (metadata != null)
            {
                index.Index(fileName, metadata);
            }
		}

		public void Delete(string fileName, StorageActionsAccessor actionsAccessor = null)
		{
            NameValueCollection metadata = null;

			Action<StorageActionsAccessor> delete = accessor =>
			{
				accessor.DeleteConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName));
				metadata = accessor.GetFile(fileName, 0, 0).Metadata;
				metadata.Remove(SynchronizationConstants.RavenSynchronizationConflict);
				metadata.Remove(SynchronizationConstants.RavenSynchronizationConflictResolution);
				accessor.UpdateFileMetadata(fileName, metadata);
			};

			if (actionsAccessor != null)
			{
				delete(actionsAccessor);
			}
			else
			{
				storage.Batch(delete);
			}

            if (metadata != null)
            {
                index.Index(fileName, metadata);
            }
		}
	}
}