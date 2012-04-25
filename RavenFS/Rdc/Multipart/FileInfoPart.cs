namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Text;
	using Newtonsoft.Json;
	using Notifications;

	public class FileInfoPart : IMimePart
	{
		private readonly string fileName;
		private readonly NameValueCollection metadata;

		public FileInfoPart(string fileName, NameValueCollection metadata, string boundary)
		{
			this.fileName = fileName;
			this.metadata = metadata;
			this.Boundary = boundary;
		}

		public string ContentDisposition
		{
			get { return "form-data"; }
		}

		public string ContentType
		{
			get { throw new NotImplementedException(); }
		}

		public void CopyTo(Stream stream)
		{
			var sb = new StringBuilder();
			sb.AppendFormat("--{0}{1}", Boundary, MimeConstants.LineSeparator);
			sb.AppendFormat("Content-Disposition: {0}; filename={1}{2}{2}", ContentDisposition, fileName, MimeConstants.LineSeparator);

			sb.Append(metadata.ToString());

			byte[] buffer = Encoding.ASCII.GetBytes(sb.ToString());
			stream.Write(buffer, 0, buffer.Length);
		}

		public string Boundary { get; set; }
	}
}