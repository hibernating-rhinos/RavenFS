namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.IO;

	public interface IMimePart
	{
		string ContentDisposition { get; }

		string ContentType { get; }

		void CopyTo(Stream stream);

		String Boundary { get; set; }
	}
}