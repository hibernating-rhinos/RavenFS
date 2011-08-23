using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests
{
	public class ClientUsage : ServerTest
	{
		private readonly RavenFileSystemClient client = new RavenFileSystemClient("http://localhost:9090");

        [Fact]
        public void CanUpdateJustMetadata()
        {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;

            client.Upload("abc.txt",new NameValueCollection
                                        {
                                            {"test", "1"}
                                        }, ms).Wait();

            var updateMetadataTask = client.UpdateMetadata("abc.txt", new NameValueCollection
                                                                      {
                                                                          {"test", "2"}
                                                                      });
            updateMetadataTask.Wait();


            var metadata = client.GetMetadataFor("abc.txt");
            Assert.Equal("2", metadata.Result["test"]);
            Assert.Equal(expected, webClient.DownloadString("/files/abc.txt"));
        }
		[Fact]
		public void CanUpload()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a',1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", ms).Wait();

			Assert.Equal(expected, webClient.DownloadString("/files/abc.txt"));
		}

		[Fact]
		public void CanUploadMetadata_And_HeadMetadata()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt",new NameValueCollection
			{
				{"test", "value"},
				{"hello", "there"}
			} ,ms).Wait();


			var collection = client.GetMetadataFor("abc.txt").Result;

			Assert.Equal("value", collection["test"]);
			Assert.Equal("there", collection["hello"]);
		}


		[Fact]
		public void CanQueryMetadata()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", new NameValueCollection
			{
				{"Test", "value"},
			}, ms).Wait();


			var collection = client.Search("Test:value").Result;

			Assert.Equal(1, collection.Length);
			Assert.Equal("abc.txt", collection[0].Name);
			Assert.Equal("value", collection[0].Metadata["Test"]);
		}


		[Fact]
		public void CanDownload()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", ms).Wait();

			var ms2 = new MemoryStream();
			client.Download("abc.txt", ms2).Wait();

			ms2.Position = 0;

			var actual = new StreamReader(ms2).ReadToEnd();

			Assert.Equal(expected, actual);
		}

		[Fact]
		public void CanDownloadPartial()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 2048);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.Upload("abc.txt", ms).Wait();

			var ms2 = new MemoryStream();
			streamWriter = new StreamWriter(ms2);
			streamWriter.Write(new string('a', 1024));
			streamWriter.Flush();
			
			client.Download("abc.txt", ms2).Wait();
			ms2.Position = 0;
			var actual = new StreamReader(ms2).ReadToEnd();

			Assert.Equal(new string('a', 2048), actual);
		}
	}
}