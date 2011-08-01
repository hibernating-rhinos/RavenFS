namespace RavenFS.Storage
{
	public class FileHeader
	{
		public string Name { get; set; }
		public long TotalSize { get; set; }
		public long UploadedSize { get; set; }
	}
}