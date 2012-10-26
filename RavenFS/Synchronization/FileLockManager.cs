namespace RavenFS.Synchronization
{
	using System;
	using System.IO;
	using NLog;
	using RavenFS.Extensions;
	using RavenFS.Storage;
	using RavenFS.Util;

	public class FileLockManager
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TimeSpan defaultTimeout = TimeSpan.FromMinutes(10);
		private TimeSpan configuredTimeout;

		private TimeSpan SynchronizationTimeout(StorageActionsAccessor accessor)
		{
			bool timeoutConfigExists = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenSynchronizationLockTimeout, out configuredTimeout);

			return timeoutConfigExists ? configuredTimeout : defaultTimeout;
		}

		public void LockByCreatingSyncConfiguration(string fileName, ServerInfo sourceServer, StorageActionsAccessor accessor)
		{
			var syncLock = new SynchronizationLock
											{
												SourceServer = sourceServer,
												FileLockedAt = DateTime.UtcNow
											};

			accessor.SetConfig(RavenFileNameHelper.SyncLockNameForFile(fileName), syncLock.AsConfig());

			log.Debug("File '{0}' was locked", fileName);
		}

		public void UnlockByDeletingSyncConfiguration(string fileName, StorageActionsAccessor accessor)
		{
			accessor.DeleteConfig(RavenFileNameHelper.SyncLockNameForFile(fileName));
			log.Debug("File '{0}' was unlocked", fileName);
		}

		public bool TimeoutExceeded(string fileName, StorageActionsAccessor accessor)
		{
			SynchronizationLock syncLock;

			try
			{
				syncLock =
					accessor.GetConfig(RavenFileNameHelper.SyncLockNameForFile(fileName)).AsObject<SynchronizationLock>();
			}
			catch (FileNotFoundException)
			{
				return true;
			}

			return DateTime.UtcNow - syncLock.FileLockedAt > SynchronizationTimeout(accessor);
		}

		public bool TimeoutExceeded(string fileName, TransactionalStorage storage)
		{
			var result = false;

			storage.Batch(accessor => result = TimeoutExceeded(fileName, accessor));

			return result;
		}
	}
}