namespace RavenFS.Util
{
	using System;

	public static class SynchronizationNamesHelper
	{
	    public const string SyncNamePrefix = "Syncing-";
		public static string SyncNameForFile(string fileName, string destination)
		{
            return SyncNamePrefix + Uri.EscapeUriString(destination) + "-" + fileName;
		}

		private const string SyncLockNamePrefix = "SyncingLock-";
		public static string SyncLockNameForFile(string fileName)
		{
			return SyncLockNamePrefix + fileName;
		}

		public const string ConflictConfigNamePrefix = "Conflicted-";
        public static string ConflictConfigNameForFile(string fileName)
        {
            return ConflictConfigNamePrefix + fileName;
        }

	    public const string SyncResultNamePrefix = "SyncResult-";
        public static string SyncResultNameForFile(string fileName)
        {
            return SyncResultNamePrefix + fileName;
        }

		public const string DownloadingFileSuffix = ".downloading";
	    public static string DownloadingFileName(string fileName)
        {
			return fileName + DownloadingFileSuffix;
        }
	}
}