namespace RavenFS.Synchronization
{
    public static class SynchronizationConstants
    {
        public const string RavenReplicationSource = "Raven-Replication-Source";
        public const string RavenReplicationVersion = "Raven-Replication-Version";
        public const string RavenReplicationHistory = "Raven-Replication-History";
        public const string RavenReplicationVersionHiLo = "Raven/Replication/VersionHilo";
        public const string RavenReplicationConflict = "Raven-Replication-Conflict";
        public const string RavenReplicationConflictResolution = "Raven-Replication-Conflict-Resolution";
        public const string RavenReplicationSourcesBasePath = "Raven/Replication/Sources";
        public const string RavenReplicationDestinations = "Raven/Replication/Destinations";
        public const string RavenReplicationDestinationsBasePath = "Raven/Replication/Destinations/";
    	public const string RavenReplicationTimeout = "Raven-Replication-Timeout";
		public const string RavenReplicationLimit = "Raven-Replication-Limit";
		public const string RavenDeleteMarker = "Raven-Delete-Marker";
		public const string RavenRenameFile = "Raven-Rename-File";

        public const int ChangeHistoryLength = 50;
    }
}