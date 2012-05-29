namespace RavenFS.Client
{
	public enum SynchronizationType
	{
		Unknown = 0,
		ContentUpdate = 1,
		MetadataUpdate = 2,
		Renaming = 3,
		Deletion = 4,
	}
}