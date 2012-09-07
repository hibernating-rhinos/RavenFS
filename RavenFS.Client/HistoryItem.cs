namespace RavenFS.Client
{
	public class HistoryItem
    {
        public long Version { get; set; }
        public string ServerId { get; set; }

		public override bool Equals(object obj)
		{
			var history = obj as HistoryItem;

			if (history == null)
			{
				return false;
			}

			return Version.Equals(history.Version) && ServerId.Equals(history.ServerId);
		}

		public override int GetHashCode()
		{
			return Version.GetHashCode() ^ ServerId.GetHashCode();
		}
    }
}