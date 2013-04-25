using System;

namespace RavenFS.Synchronization
{
	public class SynchronizationLock
	{
		public ServerInfo SourceServer { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}