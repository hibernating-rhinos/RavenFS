using System.Collections.Specialized;

namespace RavenFS.Client
{
	public class FileInfo
	{
		public string Name { get; set; }
		public long TotalSize { get; set; }
		public string HuamneTotalSize { get; set; }
		public NameValueCollection Metadata { get; set; }
	}
}