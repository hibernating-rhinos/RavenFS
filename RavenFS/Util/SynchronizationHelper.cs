namespace RavenFS.Util
{
	public static class SynchronizationHelper
	{
	    private const string SyncNamePrefix = "Syncing-";
		public static string SyncNameForFile(string fileName)
		{
            return SyncNamePrefix + fileName;
		}

        public static bool IsSyncName(string name)
        {
            return name.StartsWith(SyncNamePrefix);
        }

        public static string ConflictConfigNameForFile(string fileName)
        {
            return string.Format("Conflicted-{0}", fileName);
        }

	    private const string SyncResultNamePrefix = "SyncResult-";
        public static string SyncResultNameForFile(string fileName)
        {
            return SyncResultNamePrefix + fileName;
        }

        public static bool IsSyncResultName(string name)
        {
            return name.StartsWith(SyncResultNamePrefix);
        }

	    public static string DownloadingFileName(string fileName)
        {
            return fileName + ".downloading";
        }
	}
}