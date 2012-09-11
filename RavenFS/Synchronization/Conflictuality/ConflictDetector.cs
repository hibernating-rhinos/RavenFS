namespace RavenFS.Synchronization.Conflictuality
{
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Linq;
	using Client;
	using RavenFS.Infrastructure;

	public class ConflictDetector
	{
		public ConflictItem Check(string fileName, NameValueCollection localMetadata, NameValueCollection remoteMetadata)
		{
			var localVersion = long.Parse(localMetadata[SynchronizationConstants.RavenSynchronizationVersion]);
			var localServerId = localMetadata[SynchronizationConstants.RavenSynchronizationSource];
			var localHistory = HistoryUpdater.DeserializeHistory(localMetadata);
			var currentLocalVersion = new HistoryItem() {ServerId = localServerId, Version = localVersion};

			var version = localHistory.LastOrDefault() ?? currentLocalVersion;

			var remoteVersion = long.Parse(remoteMetadata[SynchronizationConstants.RavenSynchronizationVersion]);
			var remoteServerId = remoteMetadata[SynchronizationConstants.RavenSynchronizationSource];
			var remoteConflictHistory = HistoryUpdater.DeserializeHistory(remoteMetadata);
			remoteConflictHistory.Add(new HistoryItem() {ServerId = remoteServerId, Version = remoteVersion});

			// if there are the same files or destination is direct child there are no conflicts
			if ((remoteServerId == currentLocalVersion.ServerId && remoteVersion == currentLocalVersion.Version)
				|| remoteConflictHistory.Any(item => item.ServerId == version.ServerId && item.Version == version.Version))
			{
				return null;
			}

			var localConflictHistory = new List<HistoryItem>(localHistory) {currentLocalVersion};

			return
				new ConflictItem
				{
					CurrentHistory = localConflictHistory,
					RemoteHistory = remoteConflictHistory,
					FileName = fileName,
				};
		}

		public ConflictItem CheckOnSource(string fileName, NameValueCollection localMetadata,
		                                  NameValueCollection remoteMetadata)
		{
			return Check(fileName, remoteMetadata, localMetadata);
		}
	}
}