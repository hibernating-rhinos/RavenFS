using System.Collections.Specialized;
using System.IO;
using RavenFS.Extensions;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;
using Xunit.Extensions;

namespace RavenFS.Tests
{
    public class SynchronizationTests : MultiHostTestBase
    {
		[Theory]
		[InlineData(1)]
		[InlineData(5000)]
		// [Fact(Skip = "Syncronization isn't supported right now, we don't have a valid implementation for it.")]
        public void Synchronize_file_with_different_beginning(int size)
        {
            var differenceChunk = new MemoryStream();
            var sw = new StreamWriter(differenceChunk);

            sw.Write("Coconut is Stupid");
            sw.Flush();

            var sourceContent = PrepareSourceStream(size);
            sourceContent.Position = 0;
            var seedContent = new CombinedStream(differenceChunk, sourceContent);
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);
            var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
            var seedMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

            seedClient.UploadAsync("test.txt", seedMetadata, seedContent).Wait();
            sourceContent.Position = 0;
            sourceClient.UploadAsync("test.txt", sourceMetadata, sourceContent).Wait();

            var result = seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.txt").Result;
            Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

            string resultMD5 = null;
            using(var resultFileContent = new MemoryStream())
            {                
                var metadata = seedClient.DownloadAsync("test.txt.result", resultFileContent).Result;
                Assert.Equal("some-value", metadata["SomeTest-metadata"]);
                resultFileContent.Position = 0;
                resultMD5 = resultFileContent.GetMD5Hash();
                resultFileContent.Position = 0;
            }
            
            sourceContent.Position = 0;
            var sourceMD5 = sourceContent.GetMD5Hash();
            sourceContent.Position = 0;
            
            Assert.True(resultMD5 == sourceMD5);
        }

		//[Theory]
		//[InlineData(1024 * 1024 * 80)]
	    [Fact(Skip = "Long test")]
		public void Big_file_test(long size)
        {
            var sourceContent = new RandomStream(size, 1);
            var seedContent = new RandomlyModifiedStream(new RandomStream(size, 1), 0.01);
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);
            var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
            var seedMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

            seedClient.UploadAsync("test.bin", seedMetadata, seedContent).Wait();           
            sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

            var result = seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin").Result;            
            Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);            
        }

        private static MemoryStream PrepareSourceStream(int lines)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);

            for (var i = 1; i <= lines; i++)
            {
                for (var j = 0; j < 100; j++)
                {
                    writer.Write(i.ToString("D4"));
                }
                writer.Write("\n");
            }
            writer.Flush();

            return ms;
        }
    }
}
