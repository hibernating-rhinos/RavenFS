using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Newtonsoft.Json;
using RavenFS.Extensions;
using RavenFS.Notifications;
using RavenFS.Rdc;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;
using Xunit.Extensions;
using System.Linq;
using System.Threading;
using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
    public class SynchronizationTests : MultiHostTestBase
    {
        [Theory]
        [InlineData(1)]
        [InlineData(5000)]
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

            SynchronizationReport result = ResolveConflictAndSynchronize("test.txt", seedClient, sourceClient);
            Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

            string resultMD5 = null;
            using (var resultFileContent = new MemoryStream())
            {
                var metadata = seedClient.DownloadAsync("test.txt", resultFileContent).Result;
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

        [Theory]
        [InlineData(1024 * 1024 * 10)]
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

            SynchronizationReport result = ResolveConflictAndSynchronize("test.bin", seedClient, sourceClient);
            Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);
        }

        [Fact]
        public void Should_download_file_from_source_if_it_doesnt_exist_on_seed()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);

            sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

            var synchronizationReport = seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin").Result;
            var resultFileMetadata = seedClient.GetMetadataForAsync("test.bin").Result;

            Assert.Equal(sourceContent1.Length, synchronizationReport.BytesCopied + synchronizationReport.BytesTransfered);
            Assert.Equal("some-value", resultFileMetadata["SomeTest-metadata"]);
        }

        [Fact]
        public void Should_modify_etag_after_upload()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceClient = NewClient(1);
            sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
            var resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;
            var etag0 = resultFileMetadata["ETag"];
            sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
            resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;
            var etag1 = resultFileMetadata["ETag"];

            Assert.False(etag0 == etag1);

        }

        [Fact]
        public void Should_be_possible_to_apply_conflict()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceClient = NewClient(1);
            sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
            var guid = Guid.NewGuid().ToString();
            sourceClient.ApplyConflictAsync("test.bin", 8, guid).Wait();
            var resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;
            var conflictItemString = sourceClient.Config.GetConfig(ReplicationHelper.ConflictConfigNameForFile("test.bin")).Result["value"];
            var conflict = new TypeHidingJsonSerializer().Parse<ConflictItem>(conflictItemString);

            Assert.Equal(true.ToString(), resultFileMetadata[ReplicationConstants.RavenReplicationConflict]);
            Assert.Equal(guid, conflict.Theirs.ServerId);
            Assert.Equal(8, conflict.Theirs.Version);
            Assert.Equal(1, conflict.Ours.Version);
        }

        [Fact]
        public void Should_mark_file_as_conflicted_when_two_differnet_versions()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
            var seedClient = NewClient(0);
            var sourceClient = NewClient(1);

            sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();
            seedClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

            Assert.Throws<AggregateException>(
                () => seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin").Wait());

            var resultFileMetadata = seedClient.GetMetadataForAsync("test.bin").Result;
            Assert.True(Convert.ToBoolean(resultFileMetadata[ReplicationConstants.RavenReplicationConflict]));
        }

        [Fact]
        public void Shold_change_history_after_upload()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceClient = NewClient(1);
            sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
            var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[ReplicationConstants.RavenReplicationHistory];
            var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

            Assert.Equal(0, history.Count);

            sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
            historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[ReplicationConstants.RavenReplicationHistory];
            history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

            Assert.Equal(1, history.Count);
            Assert.Equal(1, history[0].Version);
            Assert.NotNull(history[0].ServerId);
        }

        [Fact]
        public void Should_change_history_after_metadata_change()
        {
            var sourceContent1 = new RandomStream(10, 1);
            var sourceClient = NewClient(1);
            sourceClient.UploadAsync("test.bin", new NameValueCollection {{"test", "Change me"}}, sourceContent1).Wait();
            var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[ReplicationConstants.RavenReplicationHistory];
            var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

            Assert.Equal(0, history.Count);

            sourceClient.UpdateMetadataAsync("test.bin", new NameValueCollection { { "test", "Changed" } }).Wait();
            var metadata = sourceClient.GetMetadataForAsync("test.bin").Result;
            historySerialized = metadata[ReplicationConstants.RavenReplicationHistory];
            history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

            Assert.Equal(1, history.Count);
            Assert.Equal(1, history[0].Version);
            Assert.NotNull(history[0].ServerId);
            Assert.Equal("Changed", metadata["test"]);
        }


        [Fact]
        public void Should_mark_file_to_be_resolved_using_ours_strategy()
        {
            var differenceChunk = new MemoryStream();
            var sw = new StreamWriter(differenceChunk);

            sw.Write("Coconut is Stupid");
            sw.Flush();

            var sourceContent = PrepareSourceStream(10);
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

            try
            {
                seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.txt").Wait();
            }
            catch
            {
                // pass
            }
            seedClient.ResolveConflictAsync(sourceClient.ServerUrl, "test.txt", ConflictResolutionStrategy.Ours).Wait();
            var result = seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "test.txt").Result;
            Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

            // check if conflict resolution has been properly set on the source

            string resultMd5;
            using (var resultFileContent = new MemoryStream())
            {
                var metadata = seedClient.DownloadAsync("test.txt", resultFileContent).Result;
                Assert.Equal("some-value", metadata["SomeTest-metadata"]);
                resultFileContent.Position = 0;
                resultMd5 = resultFileContent.GetMD5Hash();
                resultFileContent.Position = 0;
            }

            sourceContent.Position = 0;
            var sourceMd5 = sourceContent.GetMD5Hash();
            sourceContent.Position = 0;

            Assert.True(resultMd5 == sourceMd5);
        }

        private static SynchronizationReport ResolveConflictAndSynchronize(string fileName, RavenFileSystemClient seedClient, RavenFileSystemClient sourceClient)
        {
            try
            {
                seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, fileName).Wait();
            }
            catch
            {
                // pass
            }
            seedClient.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.Theirs).Wait();
            return seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, fileName).Result;
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
