namespace RavenFS.Synchronization
{
	using System;
	using NLog;
	using RavenFS.Extensions;
	using RavenFS.Storage;
	using RavenFS.Util;

	public class FileLockManager
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

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

			log.Debug("File '{0}' was locked at {1}", fileName, syncOperationDetails.FileLockedAt);
		}

		public void UnlockByDeletingSyncConfiguration(string fileName, StorageActionsAccessor accessor)
		{
			accessor.DeleteConfig(SynchronizationHelper.SyncLockNameForFile(fileName));
			log.Debug("File '{0}' was unlocked", fileName);
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