namespace RavenFS.Synchronization.Conflictuality
{
	using RavenFS.Client;

	public class ConflictResolution
    {
        public ConflictResolutionStrategy Strategy { get; set; }
        public long Version { get; set; }
        public string RemoteServerId { get; set; }
    }
}