namespace RavenFS.Synchronization
{
	using System;

	public class SynchronizationLock
	{
		public Guid SourceServerId { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}