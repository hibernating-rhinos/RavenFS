using System;

namespace RavenFS.Client
{
    public class SynchronizationDetails
	{
		public string SourceUrl { get; set; }

		public DateTime FileLockedAt { get; set; }
	}
}