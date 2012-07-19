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
					metadata[SynchronizationConstants.RavenSynchronizationConflict] = "True";
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
					metadata.Remove(SynchronizationConstants.RavenSynchronizationConflict);
					metadata.Remove(SynchronizationConstants.RavenSynchronizationConflictResolution);
					accessor.UpdateFileMetadata(fileName, metadata);
				});
		}
	}
}