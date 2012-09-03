using System.Collections.Specialized;
using RavenFS.Search;

namespace RavenFS.Synchronization.Conflictuality
{
	using Client;
	using RavenFS.Extensions;
	using RavenFS.Storage;
	using RavenFS.Util;

	public class ConflictActifactManager
	{
		private readonly TransactionalStorage storage;
	    private readonly IndexStorage index;

	    public ConflictActifactManager(TransactionalStorage storage, IndexStorage index)
		{
		    this.storage = storage;
		    this.index = index;
		}

	    public void CreateArtifact(string fileName, ConflictItem conflict)
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

		public void RemoveArtifact(string fileName)
		{
            NameValueCollection metadata = null;

			storage.Batch(
				accessor =>
				{
					accessor.DeleteConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName));
					metadata = accessor.GetFile(fileName, 0, 0).Metadata;
					metadata.Remove(SynchronizationConstants.RavenSynchronizationConflict);
					metadata.Remove(SynchronizationConstants.RavenSynchronizationConflictResolution);
					accessor.UpdateFileMetadata(fileName, metadata);
				});

            if (metadata != null)
            {
                index.Index(fileName, metadata);
            }
		}
	}
}