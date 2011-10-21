using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;
using FileInfo = RavenFS.Client.FileInfo;

namespace RavenFS.Tests.Bugs
{
	public class Queries : ServerTest
	{
		private readonly RavenFileSystemClient client = new RavenFileSystemClient("http://localhost:9090");


		[Fact]
		public void CanQueryMultipleFiles()
		{

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", new NameValueCollection(), ms).Wait();

			ms.Position = 0;
			client.Upload("CorelVBAManual.PDF", new NameValueCollection
			{
				{"Filename", "CorelVBAManual.PDF"}
			}, ms).Wait();

			ms.Position = 0;
			client.Upload("TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi", new NameValueCollection
			{
				{"Filename", "TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi"}
			}, ms).Wait();


			var fileInfos = client.Search("Filename:corelVBAManual.PDF").Result;

			Assert.Equal(1, fileInfos.Length);
			Assert.Equal("CorelVBAManual.PDF", fileInfos[0].Name);
		}

		[Fact]
		public void WillGetOneItemWhenSavingDocumentTwice()
		{

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", new NameValueCollection(), ms).Wait();

			for (int i = 0; i < 3; i++)
			{
				ms.Position = 0;
				client.Upload("CorelVBAManual.PDF", new NameValueCollection
				{
					{"Filename", "CorelVBAManual.PDF"}
				}, ms).Wait();
			}

			ms.Position = 0;
			client.Upload("TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi", new NameValueCollection
			{
				{"Filename", "TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi"}
			}, ms).Wait();


			var fileInfos = client.Search("Filename:corelVBAManual.PDF").Result;

			Assert.Equal(1, fileInfos.Length);
			Assert.Equal("CorelVBAManual.PDF", fileInfos[0].Name);
		}
	}
}