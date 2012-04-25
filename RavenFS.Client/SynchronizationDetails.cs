using System;

namespace RavenFS.Client
{
    public class SynchronizationDetails
	{
		public string DestinationUrl { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}