namespace RavenFS.Client
{
	using System;
	using System.Collections.Generic;

	public class DestinationSyncResult
	{
		public string DestinationServer { get; set; }

		public IEnumerable<SynchronizationReport> Reports { get; set; }

		public Exception Exception { get; set; }
	}
}