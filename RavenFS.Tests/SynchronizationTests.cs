using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RavenFS.Util;
using Xunit;
using Raven.Database.Extensions;

namespace RavenFS.Tests
{
    public class SynchronizationTests : MultiIisExpressTestBase
    {
        [Fact]
        public void Synchronize_file_with_different_beginning()
        {
            var differenceChunk = new MemoryStream();
            var sw = new StreamWriter(differenceChunk);

            sw.Write("Coconut is Stupid");
            sw.Flush();

            var sourceContent = PrepareSourceStream(5000);
            sourceContent.Position = 0;
            var seedContent = new CombinedStream(differenceChunk, sourceContent);
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);

            seedClient.UploadAsync("test.txt", seedContent).Wait();
            sourceContent.Position = 0;
            sourceClient.UploadAsync("test.txt", sourceContent).Wait();

            seedClient.StartSynchronizationAsync("server1", "test.txt").Wait();

            string resultMD5 = null;
            using(var result = new MemoryStream())
            {                
                seedClient.DownloadAsync("test.txt.result", result).Wait();
                result.Position = 0;
                resultMD5 = result.GetMD5Hash();
                result.Position = 0;
                using(var f = File.Create(@"c:\temp\result.txt"))
                {
                    result.CopyTo(f);
                }
            }
            
            sourceContent.Position = 0;
            var sourceMD5 = sourceContent.GetMD5Hash();
            sourceContent.Position = 0;
            using (var f = File.Create(@"c:\temp\source.txt"))
            {
                sourceContent.CopyTo(f);
            }
            
            Assert.True(resultMD5 == sourceMD5);

        }

        [Fact]
        public void Synchronize_small_file()
        {
            var differenceChunk = new MemoryStream();
            var sw = new StreamWriter(differenceChunk);

            sw.Write("Coconut is Stupid");
            sw.Flush();

            var sourceContent = PrepareSourceStream(1);
            sourceContent.Position = 0;
            var seedContent = new CombinedStream(differenceChunk, sourceContent);
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);

            seedClient.UploadAsync("test.txt", seedContent).Wait();
            sourceContent.Position = 0;
            sourceClient.UploadAsync("test.txt", sourceContent).Wait();

            seedClient.StartSynchronizationAsync("server1", "test.txt").Wait();

            string resultMD5 = null;
            using (var result = new MemoryStream())
            {
                seedClient.DownloadAsync("test.txt.result", result).Wait();
                result.Position = 0;
                resultMD5 = result.GetMD5Hash();
                result.Position = 0;
                using (var f = File.Create(@"c:\temp\result.txt"))
                {
                    result.CopyTo(f);
                }
            }

            sourceContent.Position = 0;
            var sourceMD5 = sourceContent.GetMD5Hash();
            sourceContent.Position = 0;
            using (var f = File.Create(@"c:\temp\source.txt"))
            {
                sourceContent.CopyTo(f);
            }

            Assert.True(resultMD5 == sourceMD5);

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
