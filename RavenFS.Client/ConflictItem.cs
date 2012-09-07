namespace RavenFS.Client
{
	using System.Collections.Generic;

	public class ConflictItem
    {
        public HistoryItem Remote { get; set; }
        public HistoryItem Current { get; set; }
		public IList<HistoryItem> RemoteHistory { get; set; }
		public IList<HistoryItem> CurrentHistory { get; set; }
		public string FileName { get; set; }
    }
}