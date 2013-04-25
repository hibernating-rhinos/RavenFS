using System;
using System.Collections.Generic;

namespace RavenFS.Client
{
	public class DestinationSyncResult
	{
		public string DestinationServer { get; set; }

		public IEnumerable<SynchronizationReport> Reports { get; set; }

		public Exception Exception { get; set; }
	}
}