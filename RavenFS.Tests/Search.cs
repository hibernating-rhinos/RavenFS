using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace RavenFS.Tests
{
    public class Search : WebApiTest
    {
        [Fact]
        public void CanSearchForFilesBySize()
        {
            var client = NewClient();

            client.UploadAsync("1", StreamOfLength(1)).Wait();
            client.UploadAsync("2", StreamOfLength(2)).Wait();
            client.UploadAsync("3", StreamOfLength(3)).Wait();
            client.UploadAsync("4", StreamOfLength(4)).Wait();
            client.UploadAsync("5", StreamOfLength(5)).Wait();

            var files = client.SearchAsync("__size_numeric:[2 TO 4]").Result.Files;
            var fileNames = files.Select(f => f.Name).ToArray();

            Assert.Equal(new[] {"2", "3", "4"}, fileNames);
        }

        [Fact]
        public void CanSearchForFilesBySizeWithWildcardMin()
        {
            var client = NewClient();

            client.UploadAsync("1", StreamOfLength(1)).Wait();
            client.UploadAsync("2", StreamOfLength(2)).Wait();
            client.UploadAsync("3", StreamOfLength(3)).Wait();
            client.UploadAsync("4", StreamOfLength(4)).Wait();
            client.UploadAsync("5", StreamOfLength(5)).Wait();

            var files = client.SearchAsync("__size_numeric:[* TO 4]").Result.Files;
            var fileNames = files.Select(f => f.Name).ToArray();

            Assert.Equal(new[] { "1", "2", "3", "4" }, fileNames);
        }

        [Fact]
        public void CanSearchForFilesBySizeWithWildcardMax()
        {
            var client = NewClient();

            client.UploadAsync("1", StreamOfLength(1)).Wait();
            client.UploadAsync("2", StreamOfLength(2)).Wait();
            client.UploadAsync("3", StreamOfLength(3)).Wait();
            client.UploadAsync("4", StreamOfLength(4)).Wait();
            client.UploadAsync("5", StreamOfLength(5)).Wait();

            var files = client.SearchAsync("__size_numeric:[3 TO *]").Result.Files;
            var fileNames = files.Select(f => f.Name).ToArray();

            Assert.Equal(new[] { "3", "4", "5" }, fileNames);
        }

        private Stream StreamOfLength(int length)
        {
            var memoryStream = new MemoryStream(Enumerable.Range(0, length).Select(i => (byte)i).ToArray());

            return memoryStream;
        }
    }
}
