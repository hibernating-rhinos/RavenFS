using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RavenFS.Util;
using Xunit;

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

            var sourceContent = PrepareSourceStream();
            sourceContent.Position = 0;
            var seedContent = new CombinedStream(differenceChunk, sourceContent);
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);

            seedClient.UploadAsync("test.txt", seedContent).Wait();
            sourceContent.Position = 0;
            sourceClient.UploadAsync("test.txt", sourceContent).Wait();

            seedClient.StartSynchronizationAsync("server1", "test.txt").Wait();
                  
            using(var f = File.Create(@"c:\temp\result.txt"))
            using(var result = new MemoryStream())
            {
                
                result.Position = 0;
                seedClient.DownloadAsync("test.txt.result", result).Wait();
                result.Position = 0;
                result.CopyTo(f);
            }
            using (var f = File.Create(@"c:\temp\source.txt"))
            {
                sourceContent.Position = 0;
                sourceContent.CopyTo(f);
            }
            using (var f = File.Create(@"c:\temp\seed.txt"))
            {
                seedContent.Position = 0;
                seedContent.CopyTo(f);
            }
            sourceContent.Position = 0;

        }

        private static MemoryStream PrepareSourceStream()
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);

            for (var i = 1; i <= 5000; i++)
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
