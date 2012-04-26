namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.IO;
	using System.Text;

	public class SeedFilePart : IFilePart
	{
		private readonly long @from;
		private readonly long length;

		public SeedFilePart(long from, long length, string boundary)
		{
			this.@from = from;
			this.length = length;
			this.Boundary = boundary;
		}

		public string ContentDisposition
		{
			get { return "form-data"; }
		}

		public string ContentType
		{
			get { return "plain/text"; }
		}

		public void CopyTo(Stream stream)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{1}--{0}{1}", Boundary, MimeConstants.LineSeparator);
			sb.AppendFormat("Content-Disposition: {0}; {1}={2}; {3}={4}; {5}={6}{7}", ContentDisposition,
							SyncingMultipartConstants.SyncingNeedType, SyncingNeedType,
							SyncingMultipartConstants.SyncingRangeFrom, SyncingRangeFrom,
							SyncingMultipartConstants.SyncingRangeTo, SyncingRangeTo,
							MimeConstants.LineSeparator);
			sb.AppendFormat("Content-Type: {0}{1}{2}", ContentType, MimeConstants.LineSeparator, MimeConstants.LineSeparator);

			byte[] buffer = Encoding.ASCII.GetBytes(sb.ToString());
			stream.Write(buffer, 0, buffer.Length);
		}

		public string Boundary { get; set; }

		public string SyncingNeedType
		{
			get { return "seed"; }
		}

		public long SyncingRangeFrom
		{
			get { return from; }
		}

		public long SyncingRangeTo
		{
			get { return from + length; }
		}
	}
}