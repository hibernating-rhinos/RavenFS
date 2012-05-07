namespace RavenFS.Rdc.Conflictuality
{
	using System.Collections.Specialized;
	using System.Linq;
	using Infrastructure;

	public class ConflictDetector
	{
		public ConflictItem Check(NameValueCollection destinationMetadata, NameValueCollection sourceMetadata)
		{
			var sourceHistory = HistoryUpdater.DeserializeHistory(sourceMetadata);
			var sourceVersion = long.Parse(sourceMetadata[SynchronizationConstants.RavenReplicationVersion]);
			var sourceServerId = sourceMetadata[SynchronizationConstants.RavenReplicationSource];
			var destinationVersion = long.Parse(destinationMetadata[SynchronizationConstants.RavenReplicationVersion]);
			var destinationServerId = destinationMetadata[SynchronizationConstants.RavenReplicationSource];
			// if there are the same files or destination is direct child there are no conflicts
			if ((sourceServerId == destinationServerId && sourceVersion == destinationVersion)
				|| sourceHistory.Any(item => item.ServerId == destinationServerId && item.Version == destinationVersion))
			{
				return null;
			}
			return
				new ConflictItem
				{
					Current = new HistoryItem { ServerId = destinationServerId, Version = destinationVersion },
					Remote = new HistoryItem { ServerId = sourceServerId, Version = sourceVersion }
				};
		}
	}
}