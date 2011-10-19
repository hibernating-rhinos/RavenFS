using System.Collections.Specialized;

namespace RavenFS.Storage
{
	public class FileHeader
	{
		public string Name { get; set; }
		public long TotalSize { get; set; }
		public long UploadedSize { get; set; }

		public string HumaneTotalSize
		{
			get { return Humane(TotalSize); }
		}


		public string HumaneUploadedSize
		{
			get { return Humane(UploadedSize); }
		}

		public static string Humane(long size)
		{
			const double GB = 1024 * 1024 * 1024;
			const double MB = 1024 * 1024 ;
			const double KB = 1024;

			if (size > GB) // GB
				return string.Format("{0:#,#.##} GBytes", size / GB);
			if(size > MB)
				return string.Format("{0:#,#.##} MBytes", size / MB);
			if (size > MB)
				return string.Format("{0:#,#.##} KBytes", size / KB);
			return string.Format("{0:#,#} Bytes", size);

		}

		public NameValueCollection Metadata { get; set; }
	}
}