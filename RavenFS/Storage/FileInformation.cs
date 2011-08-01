using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace RavenFS.Storage
{
	public class FileInformation
	{
		public string Name { get; set; }
		public NameValueCollection Metadata { get; set; }
		public int Start { get; set; }

		public long TotalSize { get; set; }
		public long UploadedSize { get; set; }

		public List<PageInformation> Pages { get; set; }

		public FileInformation()
		{
			Pages = new List<PageInformation>();
			Metadata = new NameValueCollection();
		}
	}
}