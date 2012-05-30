namespace RavenFS.Synchronization.Conflictuality
{
	using RavenFS.Extensions;
	using RavenFS.Storage;
	using RavenFS.Util;

	public class ConflictActifactManager
	{
		private readonly TransactionalStorage storage;

		public ConflictActifactManager(TransactionalStorage storage)
		{
			this.storage = storage;
		}

		public void CreateArtifact(string fileName, ConflictItem conflict)
		{
			storage.Batch(
				accessor =>
				{
					var metadata = accessor.GetFile(fileName, 0, 0).Metadata;
					accessor.SetConfigurationValue(
						SynchronizationHelper.ConflictConfigNameForFile(fileName), conflict);
					metadata[SynchronizationConstants.RavenReplicationConflict] = "True";
					accessor.UpdateFileMetadata(fileName, metadata);
				});
		}

		public void RemoveArtifact(string fileName)
		{
			storage.Batch(
				accessor =>
				{
					accessor.DeleteConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName));
					var metadata = accessor.GetFile(fileName, 0, 0).Metadata;
					metadata.Remove(SynchronizationConstants.RavenReplicationConflict);
					metadata.Remove(SynchronizationConstants.RavenReplicationConflictResolution);
					accessor.UpdateFileMetadata(fileName, metadata);
				});
		}
	}
}