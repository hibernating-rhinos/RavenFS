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
		private TimeSpan configuredTimeout;

		private TimeSpan ReplicationTimeout(StorageActionsAccessor accessor)
		{
			bool timeoutConfigExists = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationTimeout, out configuredTimeout);

			return timeoutConfigExists ? configuredTimeout : defaultTimeout;
		}

		public void LockByCreatingSyncConfiguration(string fileName, string sourceServerUrl, StorageActionsAccessor accessor)
		{
			var syncOperationDetails = new SynchronizationLock
											{
												SourceUrl = sourceServerUrl,
												FileLockedAt = DateTime.UtcNow
											};

			accessor.SetConfigurationValue(SynchronizationHelper.SyncLockNameForFile(fileName), syncOperationDetails);
		}

		public void UnlockByDeletingSyncConfiguration(string fileName, StorageActionsAccessor accessor)
		{
			accessor.DeleteConfig(SynchronizationHelper.SyncLockNameForFile(fileName));
		}

		public bool TimeoutExceeded(string fileName, StorageActionsAccessor accessor)
		{
			SynchronizationLock syncOperationDetails;
			
			if (!accessor.TryGetConfigurationValue(SynchronizationHelper.SyncLockNameForFile(fileName), out syncOperationDetails))
				return true;

			return DateTime.UtcNow - syncOperationDetails.FileLockedAt > ReplicationTimeout(accessor);
		}
	}
}