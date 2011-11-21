using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests.Bugs
{
	public class UpdatingMetadata : ServerTest
	{
		private readonly RavenFileSystemClient client = new RavenFileSystemClient("http://localhost:9090");


		[Fact]
		public void CanUpdateMetadata()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.UploadAsync("abc.txt", new NameValueCollection
			{
				{"test", "1"}
			}, ms).Wait();

			client.UpdateMetadataAsync("abc.txt", new NameValueCollection
			{
				{"test", "2"}
			});

			var metadataFor = client.GetMetadataForAsync("abc.txt");


			Assert.Equal("2", metadataFor.Result["test"]);
		}

		 
	}
}