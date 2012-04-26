using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using RavenFS.Extensions;
using RavenFS.Notifications;
using RavenFS.Rdc;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;
using Xunit.Extensions;
using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
	using System.Net;

	public class SynchronizationTestsAfterChanges : MultiHostTestBase
	{
		[Theory]
		//[InlineData(1)]
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

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection()
        	                                                                                     	{
        	                                                                                     		{ "url", "http://arek-win:19079" }
        	                                                                                     	}).Wait();

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, seedClient, "test.txt");


			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

			string resultMd5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				var metadata = seedClient.DownloadAsync("test.txt", resultFileContent).Result;
				Assert.Equal("some-value", metadata["SomeTest-metadata"]);
				resultFileContent.Position = 0;
				resultMd5 = resultFileContent.GetMD5Hash();
				resultFileContent.Position = 0;

				resultFileContent.Position = 0;
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();
			sourceContent.Position = 0;

			Assert.True(resultMd5 == sourceMd5);
		}

		[Theory]
		[InlineData(1024 * 1024 * 10)]
		public void Big_file_test(long size)
		{
			var sourceContent = new RandomStream(size, 1);
			var seedContent = new RandomlyModifiedStream(new RandomStream(size, 1), 0.01, 1);
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

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, seedClient, "test.bin");
			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);
		}

		//[Fact]
		//public void Should_download_file_from_source_if_it_doesnt_exist_on_seed()
		//{
		//    var sourceContent1 = new RandomStream(10, 1);
		//    var sourceMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "some-value"}
		//                       };
		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);

		//    sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

		//    var synchronizationReport = RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.bin");
		//    var resultFileMetadata = seedClient.GetMetadataForAsync("test.bin").Result;

		//    Assert.Equal(sourceContent1.Length, synchronizationReport.BytesCopied + synchronizationReport.BytesTransfered);
		//    Assert.Equal("some-value", resultFileMetadata["SomeTest-metadata"]);
		//}

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

		//[Fact]
		//public void Should_mark_file_as_conflicted_when_two_differnet_versions()
		//{
		//    var sourceContent1 = new RandomStream(10, 1);
		//    var sourceMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "some-value"}
		//                       };
		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);

		//    sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();
		//    seedClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

		//    var synchronizationReport = RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.bin");

		//    Assert.NotNull(synchronizationReport.Exception);
		//    var resultFileMetadata = seedClient.GetMetadataForAsync("test.bin").Result;
		//    Assert.True(Convert.ToBoolean(resultFileMetadata[SynchronizationConstants.RavenReplicationConflict]));
		//}

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
			var seedClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new RandomStream(10)).Wait();

			seedClient.UploadAsync("test.bin", new RandomStream(10)).Wait();
			var seedEtag = sourceClient.GetMetadataForAsync("test.bin").Result["ETag"];

			//RdcTestUtils.ResolveConflictAndSynchronize("test.bin", seedClient, sourceClient);

			var result = seedClient.GetMetadataForAsync("test.bin").Result["ETag"];

			Assert.True(seedEtag != result, "Etag should be updated");
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
		//    var seedContent = new CombinedStream(differenceChunk, sourceContent);
		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);
		//    var sourceMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "some-value"}
		//                       };
		//    var seedMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "shouldnt-be-overwritten"}
		//                       };

		//    seedClient.UploadAsync("test.txt", seedMetadata, seedContent).Wait();
		//    sourceContent.Position = 0;
		//    sourceClient.UploadAsync("test.txt", sourceMetadata, sourceContent).Wait();

		//    RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.txt");
		//    seedClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.txt", ConflictResolutionStrategy.Ours).Wait();
		//    var result = RdcTestUtils.SynchronizeAndWaitForStatus(sourceClient, seedClient.ServerUrl, "test.txt");
		//    Assert.Equal(seedContent.Length, result.BytesCopied + result.BytesTransfered);

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

		//    seedContent.Position = 0;
		//    var seedMd5 = seedContent.GetMD5Hash();
		//    sourceContent.Position = 0;

		//    Assert.True(resultMd5 == seedMd5);
		//}

		//[Fact]
		//public void Should_get_all_current_synchronizations()
		//{
		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);
		//    var files = new[] { "test1.bin", "test2.bin", "test3.bin" };

		//    // prepare for real synchronization
		//    foreach (var item in files)
		//    {
		//        Task.WaitAll(
		//            seedClient.UploadAsync(item, new RandomlyModifiedStream(new RandomStream(300000, 1), 0.01)),
		//            sourceClient.UploadAsync(item, new RandomStream(300000, 1)));

		//        // try to synchronize and resolve conflicts
		//        var shouldBeConflict = RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, item);
		//        Assert.NotNull(shouldBeConflict.Exception);
		//        seedClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, item, ConflictResolutionStrategy.Theirs).Wait();
		//    }

		//    // synchronize all
		//    foreach (var item in files)
		//    {
		//        seedClient.Synchronization.StartSynchronizationAsync(sourceClient.ServerUrl, item).Wait();
		//    }

		//    var result = seedClient.Synchronization.GetWorkingAsync().Result;
		//    Assert.True(0 < result.Count());
		//}

		//[Fact]
		//public void Should_get_all_finished_synchronizations()
		//{
		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);
		//    var files = new[] { "test1.bin", "test2.bin", "test3.bin" };

		//    foreach (var item in files)
		//    {
		//        Task.WaitAll(
		//            seedClient.UploadAsync(item, new RandomlyModifiedStream(new RandomStream(1000, 1), 0.01)),
		//            sourceClient.UploadAsync(item, new RandomStream(1000, 1)));

		//        RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, item);
		//    }

		//    var result = seedClient.Synchronization.GetFinishedAsync().Result;
		//    Assert.Equal(files.Length, result.Count());
		//}

		//[Fact]
		//public void When_synchronization_succeed_should_save_synchronization_source_information_config()
		//{
		//    var sourceContent1 = new RandomStream(10, 1);
		//    var sourceMetadata = new NameValueCollection
		//                       {
		//                           {"SomeTest-metadata", "some-value"}
		//                       };

		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);

		//    sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();

		//    RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.bin");

		//    var key = SynchronizationConstants.RavenReplicationSourcesBasePath + "/" + sourceClient.ServerUrl;
		//    var task = seedClient.Config.GetConfig(key);
		//    var synchronizationSourceInfoString = task.Result["value"];
		//    var synchronizationSourceInfo = new TypeHidingJsonSerializer().Parse<SynchronizationSourceInformation>(synchronizationSourceInfoString);

		//    Assert.NotNull(synchronizationSourceInfo);
		//    Assert.NotEqual(Guid.Empty, synchronizationSourceInfo.LastDocumentEtag);
		//    Assert.NotEqual(Guid.Empty, synchronizationSourceInfo.ServerInstanceId);
		//}

		//[Fact]
		//public void Sould_return_last_etag_from_source_after_synchronization()
		//{
		//    var sourceContent1 = new RandomStream(10, 1);
		//    var sourceMetadata = new NameValueCollection
		//                            {
		//                                {"SomeTest-metadata", "some-value"}
		//                            };

		//    var seedClient = NewClient(0);
		//    var sourceClient = NewClient(1);

		//    sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent1).Wait();
		//    var sourceFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;

		//    RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.bin");

		//    SynchronizationSourceInformation synchronizationSourceInfo = null;

		//    var requestUriString = seedClient.ServerUrl + "/synchronization/LastEtag?from=" + sourceClient.ServerUrl;
		//    var request = (HttpWebRequest)WebRequest.Create(requestUriString);

		//    request.GetResponseAsync()
		//        .ContinueWith(task =>
		//        {
		//            synchronizationSourceInfo =
		//                new JsonSerializer().Deserialize<SynchronizationSourceInformation>(
		//                    new JsonTextReader(new StreamReader(task.Result.GetResponseStream())));
		//        }).Wait();

		//    Assert.NotNull(synchronizationSourceInfo);
		//    Assert.Equal(sourceFileMetadata.Value<Guid>("ETag"), synchronizationSourceInfo.LastDocumentEtag);
		//    Assert.NotEqual(Guid.Empty, synchronizationSourceInfo.ServerInstanceId);
		//}

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
