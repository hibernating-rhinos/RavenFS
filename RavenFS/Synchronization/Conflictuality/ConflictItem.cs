namespace RavenFS.Synchronization.Conflictuality
{
	public class ConflictItem
    {
        public HistoryItem Remote { get; set; }
        public HistoryItem Current { get; set; }
    }
}