using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Newtonsoft.Json;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Notifications;
using RavenFS.Rdc;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;
using Xunit.Extensions;

namespace RavenFS.Tests.RDC
{
	using System.Linq;
	using System.Threading.Tasks;

	public class SynchronizationTestsAfterChanges : MultiHostTestBase
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
			var destinationContent = new CombinedStream(differenceChunk, sourceContent);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
			var destinationMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

			destinationClient.UploadAsync("test.txt", destinationMetadata, destinationContent).Wait();
			sourceContent.Position = 0;
			sourceClient.UploadAsync("test.txt", sourceMetadata, sourceContent).Wait();

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.txt");

			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

			string resultMd5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				var metadata = destinationClient.DownloadAsync("test.txt", resultFileContent).Result;
				Assert.Equal("some-value", metadata["SomeTest-metadata"]);
				resultFileContent.Position = 0;
				resultMd5 = resultFileContent.GetMD5Hash();
				resultFileContent.Position = 0;
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

			Assert.True(resultMd5 == sourceMd5);
		}

		[Theory]
		[InlineData(1024 * 1024 * 10)]
		public void Big_file_test(long size)
		{
			var sourceContent = new RandomStream(size);
			var destinationContent = new RandomlyModifiedStream(new RandomStream(size, 1), 0.01);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
			var destinationMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

			destinationClient.UploadAsync("test.bin", destinationMetadata, destinationContent).Wait();
			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");
			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);
		}

		[Theory]
		[InlineData(1024 * 1024 * 10)]
		public void Big_character_file_test(long size)
		{
			var sourceContent = new RandomCharacterStream(size);
			var destinationContent = new RandomlyModifiedStream(new RandomCharacterStream(size, 1), 0.01);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };
			var destinationMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

			destinationClient.UploadAsync("test.bin", destinationMetadata, destinationContent).Wait();
			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");
			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);
		}

		[Fact]
		public void Destination_should_know_what_is_last_file_etag_after_synchronization()
		{
			var sourceContent = new RandomStream(10, 1);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Wait();

			Guid lastEtag = destinationClient.Synchronization.GetLastEtagFromAsync(sourceClient.ServerUrl).Result;

			var sourceMetadataWithEtag = sourceClient.GetMetadataForAsync("test.bin").Result;

			Assert.Equal(sourceMetadataWithEtag.Value<Guid>("ETag"), lastEtag);
		}

		[Fact]
		public void Destination_should_not_override_last_etag_if_greater_value_exists()
		{
			var sourceContent = new RandomStream(10, 1);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			destinationClient.Config.SetConfig("Raven/Replication/Sources/http%3A%2F%2Flocalhost%3A19081",
				new NameValueCollection
			    {
			        {
			            "value",
			            "{\"LastDocumentEtag\":\"00000000-0000-0100-0000-000000000002\",\"ServerInstanceId\":\"00000000-1111-2222-3333-444444444444\"}"
			        }
			    });

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Wait();

			Guid lastEtag = destinationClient.Synchronization.GetLastEtagFromAsync(sourceClient.ServerUrl).Result;

			Assert.Equal("00000000-0000-0100-0000-000000000002", lastEtag.ToString());
		}

		[Fact]
		public void Source_should_upload_file_to_destination_if_doesnt_exist_there()
		{
			var sourceContent = new RandomStream(10, 1);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			var sourceSynchronizationReport = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;
			var resultFileMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;

			Assert.Equal(sourceContent.Length, sourceSynchronizationReport.BytesCopied + sourceSynchronizationReport.BytesTransfered);
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
			sourceClient.Synchronization.ApplyConflictAsync("test.bin", 8, guid).Wait();
			var resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;
			var conflictItemString = sourceClient.Config.GetConfig(SynchronizationHelper.ConflictConfigNameForFile("test.bin")).Result["value"];
			var conflict = new TypeHidingJsonSerializer().Parse<ConflictItem>(conflictItemString);

			Assert.Equal(true.ToString(), resultFileMetadata[SynchronizationConstants.RavenReplicationConflict]);
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
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();
			destinationClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

			var synchronizationReport =
				sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.NotNull(synchronizationReport.Exception);
			var resultFileMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;
			Assert.True(Convert.ToBoolean(resultFileMetadata[SynchronizationConstants.RavenReplicationConflict]));
		}

		[Fact]
		public void Shold_change_history_after_upload()
		{
			var sourceContent1 = new RandomStream(10, 1);
			var sourceClient = NewClient(1);
			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
			var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenReplicationHistory];
			var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

			Assert.Equal(0, history.Count);

			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent1).Wait();
			historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenReplicationHistory];
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
			sourceClient.UploadAsync("test.bin", new NameValueCollection { { "test", "Change me" } }, sourceContent1).Wait();
			var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenReplicationHistory];
			var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

			Assert.Equal(0, history.Count);

			sourceClient.UpdateMetadataAsync("test.bin", new NameValueCollection { { "test", "Changed" } }).Wait();
			var metadata = sourceClient.GetMetadataForAsync("test.bin").Result;
			historySerialized = metadata[SynchronizationConstants.RavenReplicationHistory];
			history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

			Assert.Equal(1, history.Count);
			Assert.Equal(1, history[0].Version);
			Assert.NotNull(history[0].ServerId);
			Assert.Equal("Changed", metadata["test"]);
		}

		[Fact]
		public void Should_create_new_etag_for_replicated_file()
		{
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new RandomStream(10)).Wait();

			destinationClient.UploadAsync("test.bin", new RandomStream(10)).Wait();
			var destinationEtag = sourceClient.GetMetadataForAsync("test.bin").Result["ETag"];

			RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

			var result = destinationClient.GetMetadataForAsync("test.bin").Result["ETag"];

			Assert.True(destinationEtag != result, "Etag should be updated");
		}


		//[Fact]
		//public void Should_mark_file_to_be_resolved_using_ours_strategy()
		//{
		//    var differenceChunk = new MemoryStream();
		//    var sw = new StreamWriter(differenceChunk);

		//    sw.Write("Coconut is Stupid");
		//    sw.Flush();

		//    var sourceContent = PrepareSourceStream(10);
		//    sourceContent.Position = 0;
		//    var destinationContent = new CombinedStream(differenceChunk, sourceContent);
		//    var destinationClient = NewClient(0);
		//    var sourceClient = NewClient(1);
		//    var sourceMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "some-value"}
		//                       };
		//    var destinationMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "shouldnt-be-overwritten"}
		//                       };

		//    destinationClient.UploadAsync("test.txt", destinationMetadata, destinationContent).Wait();
		//    sourceContent.Position = 0;
		//    sourceClient.UploadAsync("test.txt", sourceMetadata, sourceContent).Wait();

		//    RdcTestUtils.SynchronizeAndWaitForStatus(destinationClient, sourceClient.ServerUrl, "test.txt");
		//    destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.txt", ConflictResolutionStrategy.Ours).Wait();
		//    var result = RdcTestUtils.SynchronizeAndWaitForStatus(sourceClient, destinationClient.ServerUrl, "test.txt");
		//    Assert.Equal(destinationContent.Length, result.BytesCopied + result.BytesTransfered);

		//    // check if conflict resolution has been properly set on the source
		//    string resultMd5;
		//    using (var resultFileContent = new MemoryStream())
		//    {
		//        var metadata = sourceClient.DownloadAsync("test.txt", resultFileContent).Result;
		//        Assert.Equal("shouldnt-be-overwritten", metadata["SomeTest-metadata"]);
		//        resultFileContent.Position = 0;
		//        resultMd5 = resultFileContent.GetMD5Hash();
		//        resultFileContent.Position = 0;
		//    }

		//    destinationContent.Position = 0;
		//    var destinationMd5 = destinationContent.GetMD5Hash();
		//    sourceContent.Position = 0;

		//    Assert.True(resultMd5 == destinationMd5);
		//}

		[Fact(Skip = "Race condition")]
		public void Should_get_all_current_synchronizations()
		{
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var files = new[] { "test1.bin", "test2.bin", "test3.bin" };

			// prepare for real synchronization
			foreach (var item in files)
			{
				Task.WaitAll(
					destinationClient.UploadAsync(item, new RandomlyModifiedStream(new RandomStream(300000, 1), 0.01)),
					sourceClient.UploadAsync(item, new RandomStream(300000, 1)));

				// try to synchronize and resolve conflicts
				var shouldBeConflict =
					sourceClient.Synchronization.StartSynchronizationToAsync(item, destinationClient.ServerUrl).Result;
				Assert.NotNull(shouldBeConflict.Exception);
				destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, item, ConflictResolutionStrategy.RemoteVersion).Wait();
			}

			// synchronize all
			foreach (var item in files)
			{
				sourceClient.Synchronization.StartSynchronizationToAsync(item, destinationClient.ServerUrl);
			}

			var result = destinationClient.Synchronization.GetWorkingAsync().Result;
			Assert.True(0 < result.Count());
		}

		[Fact]
		public void Should_get_all_finished_synchronizations()
		{
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var files = new[] { "test1.bin", "test2.bin", "test3.bin" };

			foreach (var item in files)
			{
				Task.WaitAll(
					destinationClient.UploadAsync(item, new RandomlyModifiedStream(new RandomStream(1000, 1), 0.01)),
					sourceClient.UploadAsync(item, new RandomStream(1000, 1)));

				RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, item);
			}

			var result = destinationClient.Synchronization.GetFinishedAsync().Result;
			Assert.Equal(files.Length, result.Count());
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
