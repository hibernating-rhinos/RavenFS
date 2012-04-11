namespace RavenFS.Util
{
	public static class ReplicationHelper
	{
		public static string SyncConfigNameForFile(string fileName)
		{
			return string.Format("SYNC-{0}", fileName);
		}

        public static string ConflictConfigNameForFile(string fileName)
        {
            return string.Format("CONFLICT-{0}", fileName);
        }
	}
}