namespace RavenFS.Rdc
{
	using System;

	public class SynchronizationDetails
	{
		public string ReplicationSource { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}