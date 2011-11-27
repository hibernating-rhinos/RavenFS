using System.Collections.Specialized;

namespace RavenFS.Client
{

	public class ServerStats
	{
		public long FileCount { get; set; }
	}

	public class FileInfo
	{
		public string Name { get; set; }
		public long TotalSize { get; set; }
		public string HumaneTotalSize { get; set; }
		public NameValueCollection Metadata { get; set; }
	}
}