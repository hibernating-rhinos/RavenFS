namespace RavenFS.Rdc
{
	using System;
	using Extensions;
	using Storage;
	using Util;

	public class FileLockManager
	{
		private readonly TimeSpan defaultTimeout = new TimeSpan(0, 0, 10, 0); // TODO: set up default timestamp
		private readonly TransactionalStorage storage;
		private TimeSpan configuredTimeout;

		public FileLockManager(TransactionalStorage storage)
		{
			this.storage = storage;
		}

		private TimeSpan ReplicationTimeout
		{
			get
			{
				bool timeoutConfigExists = false;
				storage.Batch(accessor => timeoutConfigExists = accessor.TryGetConfigurationValue(ReplicationConstants.RavenReplicationTimeout, out configuredTimeout));

				return timeoutConfigExists ? configuredTimeout : defaultTimeout;
			}
		}

		public void LockByCreatingSyncConfiguration(string fileName, string sourceServerUrl)
		{
			storage.Batch(accessor =>
			{
				var syncOperationDetails = new SynchronizationDetails
				                          	{
				                          		ReplicationSource = sourceServerUrl,
				                          		FileLockedAt = DateTime.UtcNow
				                          	};

				accessor.SetConfigurationValue(ReplicationHelper.SyncConfigNameForFile(fileName),
				                                                       syncOperationDetails);
			});
		}

		public void UnlockByDeletingSyncConfiguration(string fileName)
		{
			storage.Batch(accessor => accessor.DeleteConfig(ReplicationHelper.SyncConfigNameForFile(fileName)));
		}

		public bool IsFileBeingLocked(string fileName)
		{
			bool result = false;
			storage.Batch(accessor => result = accessor.ConfigExists(ReplicationHelper.SyncConfigNameForFile(fileName)));
			return result;
		}

		public bool TimeoutExceeded(string fileName)
		{
			SynchronizationDetails syncOperationDetails = null;
			storage.Batch(accessor => accessor.TryGetConfigurationValue(ReplicationHelper.SyncConfigNameForFile(fileName), out syncOperationDetails));

			return DateTime.UtcNow - syncOperationDetails.FileLockedAt > ReplicationTimeout;
		}
	}
}