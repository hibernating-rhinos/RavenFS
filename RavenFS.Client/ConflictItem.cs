namespace RavenFS.Client
{
	public class ConflictItem
    {
        public HistoryItem Remote { get; set; }
        public HistoryItem Current { get; set; }
		public string FileName { get; set; }
    }
}