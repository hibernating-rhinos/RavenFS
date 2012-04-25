namespace RavenFS.Rdc.Multipart
{
	public interface IFilePart : IMimePart
	{
		string SyncingNeedType { get; }

		long SyncingRangeFrom { get; }

		long SyncingRangeTo { get; }
	}
}