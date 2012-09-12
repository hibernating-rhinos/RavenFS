namespace RavenFS.Synchronization
{
	using System.ComponentModel;

	public enum NoSyncReason
	{
		Unknown = 0,
		[Description("There were the same content and metadata")]
		SameContentAndMetadata = 1,
		[Description("Destination server had this file in the past")]
		ContainedInDestHistory = 2,
	}
}