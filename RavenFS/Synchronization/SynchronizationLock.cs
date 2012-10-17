namespace RavenFS.Synchronization
{
	using System;

	public class SynchronizationLock
	{
		public ServerInfo SourceServer { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}