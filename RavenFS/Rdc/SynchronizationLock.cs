namespace RavenFS.Rdc
{
	using System;

	public class SynchronizationLock
	{
		public string SourceUrl { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}