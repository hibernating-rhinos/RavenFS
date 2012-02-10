using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RavenFS.Util;
using Xunit;

namespace RavenFS.Tests
{
    public class NarrowedStreamTests
    {
        [Fact]
        public void Check_reading_from_begining()
        {
            var ms = PrepareSourceStream();
            var tested = new NarrowedStream(ms, 0, 5);

            var reader = new StreamReader(tested);
            var result = reader.ReadToEnd();
            Assert.Equal("000001", result);
        }

        [Fact]
        public void Check_reading_from_some_offset()
        {
            var ms = PrepareSourceStream();
            var tested = new NarrowedStream(ms, 1, 6);

            var reader = new StreamReader(tested);
            var result = reader.ReadToEnd();
            Assert.Equal("000010", result);
        }

        [Fact]
        public void Check_reading_all()
        {
            var ms = PrepareSourceStream();
            var tested = new NarrowedStream(ms, 0, ms.Length - 1);

            var reader = new StreamReader(tested);
            var result = reader.ReadToEnd();
            Assert.Equal(500000 * 6, result.Length);
            Assert.Equal(500000 * 6, tested.Length);
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
