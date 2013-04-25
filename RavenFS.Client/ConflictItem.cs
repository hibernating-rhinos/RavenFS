using System.Collections.Generic;

namespace RavenFS.Client
{
	public class ConflictItem
	{
		public IList<HistoryItem> RemoteHistory { get; set; }
		public IList<HistoryItem> CurrentHistory { get; set; }
		public string FileName { get; set; }
		public string RemoteServerUrl { get; set; }
	}
}