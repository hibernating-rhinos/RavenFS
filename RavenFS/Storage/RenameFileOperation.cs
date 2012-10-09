namespace RavenFS.Storage
{
	using System.Collections.Specialized;

	public class RenameFileOperation
	{
		public string Name { get; set; }

		public string Rename { get; set; }

		public NameValueCollection MetadataAfterOperation { get; set; }
	}
}