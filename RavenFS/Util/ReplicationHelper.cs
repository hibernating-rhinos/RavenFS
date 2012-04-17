namespace RavenFS.Util
{
	public static class ReplicationHelper
	{
		public static string SyncConfigNameForFile(string fileName)
		{
			return string.Format("Syncing-{0}", fileName);
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