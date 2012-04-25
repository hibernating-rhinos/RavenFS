using RavenFS.Client;

namespace RavenFS.Rdc
{
	using System;
	using Extensions;
	using Storage;
	using Util;

	public class FileLockManager
	{
		private readonly TimeSpan defaultTimeout = TimeSpan.FromMinutes(10);
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
				storage.Batch(accessor => timeoutConfigExists = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationTimeout, out configuredTimeout));

				return timeoutConfigExists ? configuredTimeout : defaultTimeout;
			}
		}

		public void LockByCreatingSyncConfiguration(string fileName, string destinationServerUrl = null)
		{
			storage.Batch(accessor =>
			{
				var syncOperationDetails = new SynchronizationDetails
				                          	{
				                          		DestinationUrl = destinationServerUrl,
				                          		FileLockedAt = DateTime.UtcNow
				                          	};

				accessor.SetConfigurationValue(SynchronizationHelper.SyncNameForFile(fileName),
				                                                       syncOperationDetails);
			});
		}

		public void UnlockByDeletingSyncConfiguration(string fileName)
		{
			storage.Batch(accessor => accessor.DeleteConfig(SynchronizationHelper.SyncNameForFile(fileName)));
		}

		public bool TimeoutExceeded(string fileName)
		{
			SynchronizationDetails syncOperationDetails = null;
			storage.Batch(accessor => accessor.TryGetConfigurationValue(SynchronizationHelper.SyncNameForFile(fileName), out syncOperationDetails));

			if (syncOperationDetails == null)
				return true;

			return DateTime.UtcNow - syncOperationDetails.FileLockedAt > ReplicationTimeout;
		}
	}
}