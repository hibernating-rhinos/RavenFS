namespace RavenFS.Synchronization.Conflictuality
{
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
			var localConflictHistory = HistoryUpdater.DeserializeHistory(localMetadata);
			localConflictHistory.Add(new HistoryItem() { ServerId = localServerId, Version = localVersion });

			var remoteVersion = long.Parse(remoteMetadata[SynchronizationConstants.RavenSynchronizationVersion]);
			var remoteServerId = remoteMetadata[SynchronizationConstants.RavenSynchronizationSource];
			var remoteSourceHistory = HistoryUpdater.DeserializeHistory(remoteMetadata);
			remoteSourceHistory.Add(new HistoryItem() {ServerId = remoteServerId, Version = remoteVersion});

			// if there are the same files or destination is direct child there are no conflicts
			if ((remoteServerId == localServerId && remoteVersion == localVersion)
				|| remoteSourceHistory.Any(item => item.ServerId == localServerId && item.Version == localVersion))
			{
				return null;
			}

			return
				new ConflictItem
				{
					CurrentHistory = localConflictHistory,
					RemoteHistory = remoteSourceHistory,
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