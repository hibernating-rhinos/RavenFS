namespace RavenFS.Util
{
	using System;

	public static class SynchronizationHelper
	{
	    private const string SyncNamePrefix = "Syncing-";
		public static string SyncNameForFile(string fileName, string destination)
		{
            return SyncNamePrefix + Uri.EscapeUriString(destination) + "-" + fileName;
		}

		public static bool IsSyncName(string name, string destination)
		{
			return name.StartsWith(SyncNamePrefix + Uri.EscapeUriString(destination));
		}

		private const string SyncLockNamePrefix = "SyncingLock-";
		public static string SyncLockNameForFile(string fileName)
		{
			return SyncLockNamePrefix + fileName;
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