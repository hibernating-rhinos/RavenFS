namespace RavenFS.Client
{
	using System.Collections.Generic;

	public class ConflictItem
    {
		public IList<HistoryItem> RemoteHistory { get; set; }
		public IList<HistoryItem> CurrentHistory { get; set; }
		public string FileName { get; set; }
    }
}