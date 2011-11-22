using System;
using System.Collections.Specialized;

namespace RavenFS.Storage
{
	public class FileHeader
	{
		public string Name { get; set; }
		public long? TotalSize { get; set; }
		public long UploadedSize { get; set; }

		public string HumaneTotalSize
		{
			get
			{
				return Humane(TotalSize);
			}
		}


		public string HumaneUploadedSize
		{
			get { return Humane(UploadedSize); }
		}

		public static string Humane(long? size)
		{
			if (size == null)
				return null;

			var absSize = Math.Abs(size.Value);
			const double GB = 1024 * 1024 * 1024;
			const double MB = 1024 * 1024 ;
			const double KB = 1024;

			if (absSize > GB) // GB
				return string.Format("{0:#,#.##} GBytes", size / GB);
			if (absSize > MB)
				return string.Format("{0:#,#.##} MBytes", size / MB);
			if (absSize > MB)
				return string.Format("{0:#,#.##} KBytes", size / KB);
			return string.Format("{0:#,#} Bytes", size);

		}

		public NameValueCollection Metadata { get; set; }
	}
}