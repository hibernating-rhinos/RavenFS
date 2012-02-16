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
            var seed = NewClient(0);
            var source = NewClient(1);

            seed.UploadAsync("test.txt", seedContent).Wait();
            sourceContent.Position = 0;
            source.UploadAsync("test.txt", sourceContent).Wait();

            var result = seed.StartSynchronizationAsync("server1", "test.txt").Result;
            Assert.NotNull(result);
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
