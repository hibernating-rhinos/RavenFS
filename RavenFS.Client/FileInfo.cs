using System.Collections.Generic;
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

    public class RdcStats
    {
        public int Version { get; set; }
    }

    public class SignatureManifest
    {
        public string FileName { get; set; }
        public IList<Signature> Signatures { get; set; }
        public long FileLength { get; set; }
    }

    public class Signature
    {     
        public string Name { get; set; }     
        public long Length { get; set; }
    }
}