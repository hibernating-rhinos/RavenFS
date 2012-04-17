using System;

namespace RavenFS.Client
{
    public class SynchronizationDetails
	{
		public string ReplicationSource { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}