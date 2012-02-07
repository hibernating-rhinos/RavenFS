using System;
using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests
{
	public class ClientUsage : IisExpressTestClient
	{
        [Fact]
        public void CanUpdateJustMetadata()
        {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;
        	var client = NewClient();
            client.UploadAsync("abc.txt",new NameValueCollection
                                        {
                                            {"test", "1"}
                                        }, ms).Wait();

            var updateMetadataTask = client.UpdateMetadataAsync("abc.txt", new NameValueCollection
                                                                      {
                                                                          {"test", "2"}
                                                                      });
            updateMetadataTask.Wait();


			var metadata = client.GetMetadataForAsync("abc.txt");
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

			var client = NewClient(); 
			client.UploadAsync("abc.txt", ms).Wait();

			var downloadString = webClient.DownloadString("/files/abc.txt");
			Assert.Equal(expected, downloadString);
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
			var client = NewClient();
			client.UploadAsync("abc.txt", new NameValueCollection
			{
				{"test", "value"},
				{"hello", "there"}
			} ,ms).Wait();


			var collection = client.GetMetadataForAsync("abc.txt").Result;

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
			var client = NewClient();
			client.UploadAsync("abc.txt", new NameValueCollection
			{
				{"Test", "value"},
			}, ms).Wait();


			var collection = client.SearchAsync("Test:value").Result;

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
			var client = NewClient();
			client.UploadAsync("abc.txt", ms).Wait();

			var ms2 = new MemoryStream();
			client.DownloadAsync("abc.txt", ms2).Wait();

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
			var client = NewClient();
			client.UploadAsync("abc.txt", ms).Wait();

			var ms2 = new MemoryStream();
			streamWriter = new StreamWriter(ms2);
			streamWriter.Write(new string('a', 1024));
			streamWriter.Flush();

			client.DownloadAsync("abc.txt", ms2).Wait();
			ms2.Position = 0;
			var actual = new StreamReader(ms2).ReadToEnd();

			Assert.Equal(new string('a', 2048), actual);
		}

        [Fact]
        public void CanCheckRdcStats()
        {
            var client = NewClient();
            var result = client.GetRdcStatsAsync().Result;
            Assert.NotNull(result);
            Assert.Equal(0x010000, result.Version);
        }

        [Fact]
        public void CanGetRdcManifest()
        {
            var client = NewClient();

            var buffer = new byte[1024 * 1024 * 2];
            new Random().NextBytes(buffer);

            webClient.UploadData("/files/mb.bin", "PUT", buffer);


            var result = client.GetRdcManifestAsync("mb.bin").Result;
            Assert.NotNull(result);
        }

        [Fact]
        public void CanGetRdcSignatures()
        {
            var client = NewClient();

            var buffer = new byte[1024 * 1024 * 2];
            new Random().NextBytes(buffer);

            webClient.UploadData("/files/mb.bin", "PUT", buffer);


            var result = client.GetRdcManifestAsync("mb.bin").Result;

            Assert.True(result.Signatures.Count > 0);

            foreach (var item in result.Signatures)
            {
                var ms = new MemoryStream();                
                client.DownloadSignatureAsync(item.Name, ms).Wait();
                Assert.True(ms.Length == item.Length);
            }                        
        }
	}
}