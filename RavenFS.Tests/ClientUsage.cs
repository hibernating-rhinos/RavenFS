using System;
using System.Collections.Specialized;
using System.IO;
using Xunit;
using Xunit.Extensions;

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
            client.UploadAsync("abc.txt", new NameValueCollection
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
            Assert.Equal(expected, WebClient.DownloadString("/files/abc.txt"));
        }
        [Theory]
        [InlineData(1024 * 1024)]		// 1 mb
        [InlineData(1024 * 1024 * 8)]	// 8 mb
        public void CanUpload(int size)
        {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', size);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;

            var client = NewClient();
            client.UploadAsync("abc.txt", ms).Wait();

            var downloadString = WebClient.DownloadString("/files/abc.txt");
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
			}, ms).Wait();


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

            WebClient.UploadData("/files/mb.bin", "PUT", buffer);


            var result = client.GetRdcManifestAsync("mb.bin").Result;
            Assert.NotNull(result);
        }

        [Fact]
        public void CanGetRdcSignatures()
        {
            var client = NewClient();

            var buffer = new byte[1024 * 1024 * 2];
            new Random().NextBytes(buffer);

            WebClient.UploadData("/files/mb.bin", "PUT", buffer);


            var result = client.GetRdcManifestAsync("mb.bin").Result;

            Assert.True(result.Signatures.Count > 0);

            foreach (var item in result.Signatures)
            {
                var ms = new MemoryStream();
                client.DownloadSignatureAsync(item.Name, ms).Wait();
                Assert.True(ms.Length == item.Length);
            }
        }

        [Fact]
        public void CanGetPartialContent_from_beggining()
        {
            var ms = PrepareSourceStream();
            ms.Position = 0;
            var client = NewClient();
            client.UploadAsync("abc.txt",
                               new NameValueCollection
                                   {
                                       {"test", "1"}
                                   }, ms)
                .Wait();
            var downloadedStream = new MemoryStream();
            var nameValues = client.DownloadAsync("/rdc/files/", "abc.txt", downloadedStream, new Tuple<long, long?>(0, 5)).Result;
            var sr = new StreamReader(downloadedStream);
            downloadedStream.Position = 0;
            var result = sr.ReadToEnd();
            Assert.Equal("000001", result);
            Assert.Equal("bytes 0-5/3000000", nameValues["Content-Range"]);
            Assert.Equal("6", nameValues["Content-Length"]);
        }

        [Fact]
        public void CanGetPartialContent_from_middle()
        {
            var ms = PrepareSourceStream();
            ms.Position = 0;
            var client = NewClient();
            client.UploadAsync("abc.txt",
                               new NameValueCollection
                                   {
                                       {"test", "1"}
                                   }, ms)
                .Wait();
            var downloadedStream = new MemoryStream();
            var nameValues = client.DownloadAsync("/rdc/files/", "abc.txt", downloadedStream, new Tuple<long, long?>(3006, 3017)).Result;
            var sr = new StreamReader(downloadedStream);
            downloadedStream.Position = 0;
            var result = sr.ReadToEnd();
            Assert.Equal("000502000503", result);
            Assert.Equal("bytes 3006-3017/3000000", nameValues["Content-Range"]);
            Assert.Equal("12", nameValues["Content-Length"]);
        }

        [Fact]
        public void CanGetPartialContent_from_end_explicitely()
        {
            var ms = PrepareSourceStream();
            ms.Position = 0;
            var client = NewClient();
            client.UploadAsync("abc.txt",
                               new NameValueCollection
                                   {
                                       {"test", "1"}
                                   }, ms)
                .Wait();
            var downloadedStream = new MemoryStream();
            var nameValues = client.DownloadAsync("/rdc/files/", "abc.txt", downloadedStream, new Tuple<long, long?>(ms.Length - 6, ms.Length - 1)).Result;
            var sr = new StreamReader(downloadedStream);
            downloadedStream.Position = 0;
            var result = sr.ReadToEnd();
            Assert.Equal("500000", result);
            Assert.Equal("bytes 2999994-2999999/3000000", nameValues["Content-Range"]);
            Assert.Equal("6", nameValues["Content-Length"]);
        }

        [Fact]
        public void CanGetPartialContent_from_end()
        {
            var ms = PrepareSourceStream();
            ms.Position = 0;
            var client = NewClient();
            client.UploadAsync("abc.txt",
                               new NameValueCollection
                                   {
                                       {"test", "1"}
                                   }, ms)
                .Wait();
            var downloadedStream = new MemoryStream();
            //var nameValues = client.DownloadAsync("/rdc/files/", "abc.txt", downloadedStream, new Tuple<long, long>(ms.Length - 7, ms.Length - 2)).Result;
            var nameValues = client.DownloadAsync("/rdc/files/", "abc.txt", downloadedStream, new Tuple<long, long?>(ms.Length - 7, null)).Result;
            var sr = new StreamReader(downloadedStream);
            downloadedStream.Position = 0;
            var result = sr.ReadToEnd();
            Assert.Equal("9500000", result);
            Assert.Equal("bytes 2999993-2999999/3000000", nameValues["Content-Range"]);
            Assert.Equal("7", nameValues["Content-Length"]);
        }

        private static MemoryStream PrepareSourceStream()
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            for (var i = 1; i <= 500000; i++)
            {
                writer.Write(i.ToString("D6"));
            }
            writer.Flush();
            return ms;
        }
    }
}