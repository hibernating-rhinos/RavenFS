namespace RavenFS.Extensions
{
	using Storage;
	using Synchronization;

	public static class FileHeaderExtensions
	{
		public static bool IsBeingUploaded(this FileHeader header)
		{
			return header.TotalSize == null || header.TotalSize != header.UploadedSize ||
				   (header.Metadata[SynchronizationConstants.RavenDeleteMarker] == null && header.Metadata["Content-MD5"] == null);
		}
	}
}