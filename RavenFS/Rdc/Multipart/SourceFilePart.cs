namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.IO;
	using System.Text;
	using Util;

	public class SourceFilePart : IFilePart
	{
		private readonly Stream source;
		private readonly long length;
		private readonly long @from;

		public SourceFilePart(Stream source, long from, long length, string boundary)
		{
			this.source = source;
			this.length = length;
			this.Boundary = boundary;
			this.@from = from;
		}

		public string ContentDisposition
		{
			get { return "file"; }
		}

		public string ContentType
		{
			get { return "application/octet-stream"; }
		}

		public void CopyTo(Stream stream)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{1}--{0}{1}", Boundary, MimeConstants.LineSeparator);
			sb.AppendFormat("Content-Disposition: {0}; {1}={2}; {3}={4}; {5}={6}{7}", ContentDisposition,
							SyncingMultipartConstants.NeedType, SyncingNeedType,
							SyncingMultipartConstants.RangeFrom, SyncingRangeFrom,
							SyncingMultipartConstants.RangeTo, SyncingRangeTo,
							MimeConstants.LineSeparator);
			sb.AppendFormat("Content-Type: {0}{1}{2}", ContentType, MimeConstants.LineSeparator, MimeConstants.LineSeparator);

			byte[] buffer = Encoding.ASCII.GetBytes(sb.ToString());
			stream.Write(buffer, 0, buffer.Length);

			var narrowedStream = new NarrowedStream(source, from, from + length - 1);
			narrowedStream.CopyToAsync(stream).Wait();
		}

		public string Boundary { get; set; }

		public string SyncingNeedType
		{
			get { return "source"; }
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