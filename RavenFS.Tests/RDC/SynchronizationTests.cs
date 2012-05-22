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
	using IO;
	using Rdc.Conflictuality;
	using Rdc.Utils.IO;

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

			var sourceContent = RdcTestUtils.PrepareSourceStream(size);
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
				resultMd5 = IOExtensions.GetMD5Hash(resultFileContent);
				resultFileContent.Position = 0;
			}

			sourceContent.Position = 0;
			var sourceMd5 = IOExtensions.GetMD5Hash(sourceContent);

			Assert.True(resultMd5 == sourceMd5);
		}

		[Theory]
		[InlineData(5000)]
		public void Should_have_the_same_content(int size)
		{
			var sourceContent = RdcTestUtils.PrepareSourceStream(size);
			sourceContent.Position = 0;
			var destinationContent = new RandomlyModifiedStream(sourceContent, 0.01);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			destinationClient.UploadAsync("test.txt", new NameValueCollection(), destinationContent).Wait();
			sourceContent.Position = 0;
			sourceClient.UploadAsync("test.txt", new NameValueCollection(), sourceContent).Wait();

			SynchronizationReport result = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.txt");

			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

			string resultMd5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				destinationClient.DownloadAsync("test.txt", resultFileContent).Wait();
				resultFileContent.Position = 0;
				resultMd5 = IOExtensions.GetMD5Hash(resultFileContent);
			}

			sourceContent.Position = 0;
			var sourceMd5 = IOExtensions.GetMD5Hash(sourceContent);

			Assert.Equal(sourceMd5, resultMd5);
		}

		[Theory]
		[InlineData(1024 * 1024 * 10)]
		public void Big_file_test(long size)
		{
			var sourceContent = new RandomStream(size);
			var destinationContent = new RandomlyModifiedStream(new RandomStream(size), 0.01);
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
			var destinationContent = new RandomlyModifiedStream(new RandomCharacterStream(size), 0.01);
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
			var sourceContent = new RandomStream(10);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Wait();

			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync(sourceClient.ServerUrl).Result;

			var sourceMetadataWithEtag = sourceClient.GetMetadataForAsync("test.bin").Result;

			Assert.Equal(sourceMetadataWithEtag.Value<Guid>("ETag"), lastSynchronization.LastSourceFileEtag);
		}

		[Fact]
		public void Destination_should_not_override_last_etag_if_greater_value_exists()
		{
			var sourceContent = new RandomStream(10);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test1.bin", sourceMetadata, sourceContent).Wait();
			sourceClient.UploadAsync("test2.bin", sourceMetadata, sourceContent).Wait();

			sourceClient.Synchronization.StartSynchronizationToAsync("test2.bin", destinationClient.ServerUrl).Wait();
			sourceClient.Synchronization.StartSynchronizationToAsync("test1.bin", destinationClient.ServerUrl).Wait();

			var lastSourceETag = sourceClient.GetMetadataForAsync("test2.bin").Result.Value<Guid>("ETag");
			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync(sourceClient.ServerUrl).Result;

			Assert.Equal(lastSourceETag, lastSynchronization.LastSourceFileEtag);
		}

		[Fact]
		public void Destination_should_return_empty_guid_as_last_etag_if_no_syncing_was_made()
		{
			var destinationClient = NewClient(0);

			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync("http://localhost:1234").Result;

			Assert.Equal(Guid.Empty, lastSynchronization.LastSourceFileEtag);
		}

		[Fact]
		public void Source_should_upload_file_to_destination_if_doesnt_exist_there()
		{
			var sourceContent = new RandomStream(10);
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
			var sourceContent1 = new RandomStream(10);
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
			var content = new RandomStream(10);
			var client = NewClient(1);
			client.UploadAsync("test.bin", new NameValueCollection(), content).Wait();
			var guid = Guid.NewGuid().ToString();
			client.Synchronization.ApplyConflictAsync("test.bin", 8, guid).Wait();
			var resultFileMetadata = client.GetMetadataForAsync("test.bin").Result;
			var conflictItemString = client.Config.GetConfig(SynchronizationHelper.ConflictConfigNameForFile("test.bin")).Result["value"];
			var conflict = new TypeHidingJsonSerializer().Parse<ConflictItem>(conflictItemString);

			Assert.Equal(true.ToString(), resultFileMetadata[SynchronizationConstants.RavenReplicationConflict]);
			Assert.Equal(guid, conflict.Remote.ServerId);
			Assert.Equal(8, conflict.Remote.Version);
			Assert.Equal(1, conflict.Current.Version);
		}

		[Fact]
		public void Should_throw_not_found_exception_when_applying_conflict_on_missing_file()
		{
			var client = NewClient(1);

			var guid = Guid.NewGuid().ToString();
			var innerException = RdcTestUtils.ExecuteAndGetInnerException(() => client.Synchronization.ApplyConflictAsync("test.bin", 8, guid).Wait());

			Assert.IsType(typeof(InvalidOperationException), innerException);
			Assert.Contains("404", innerException.Message);
		}

		[Fact]
		public void Should_mark_file_as_conflicted_when_two_differnet_versions()
		{
			var sourceContent = new RandomStream(10);
			var sourceMetadata = new NameValueCollection
		                       {
		                           {"SomeTest-metadata", "some-value"}
		                       };
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();
			destinationClient.UploadAsync("test.bin", sourceMetadata, sourceContent).Wait();

			var synchronizationReport =
				sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.NotNull(synchronizationReport.Exception);
			var resultFileMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;
			Assert.True(Convert.ToBoolean(resultFileMetadata[SynchronizationConstants.RavenReplicationConflict]));
		}

		[Fact]
		public void Must_not_synchronize_conflicted_file()
		{
			var sourceContent = new RandomStream(10);
			var sourceMetadataWithConflict = new NameValueCollection
		                       {
		                           {SynchronizationConstants.RavenReplicationConflict, "true"}
		                       };

			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", sourceMetadataWithConflict, sourceContent).Wait();
		
			var shouldBeConflict = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.NotNull(shouldBeConflict.Exception);
			Assert.Equal("File test.bin is conflicted", shouldBeConflict.Exception.Message);
		}

		[Fact]
		public void Shold_change_history_after_upload()
		{
			var sourceContent1 = new RandomStream(10);
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
			var sourceContent1 = new RandomStream(10);
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

		[Fact]
		public void Should_mark_file_to_be_resolved_using_current_strategy()
		{
		    var differenceChunk = new MemoryStream();
		    var sw = new StreamWriter(differenceChunk);

		    sw.Write("Coconut is Stupid");
		    sw.Flush();

		    var sourceContent = RdcTestUtils.PrepareSourceStream(10);
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
		                           {"SomeTest-metadata", "shouldnt-be-overwritten"}
		                       };

		    destinationClient.UploadAsync("test.txt", destinationMetadata, destinationContent).Wait();
		    sourceContent.Position = 0;
		    sourceClient.UploadAsync("test.txt", sourceMetadata, sourceContent).Wait();

			
			var shouldBeConflict = sourceClient.Synchronization.StartSynchronizationToAsync("test.txt", destinationClient.ServerUrl).Result;

			Assert.Equal("File test.txt is conflicted.", shouldBeConflict.Exception.Message);

			destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.txt", ConflictResolutionStrategy.CurrentVersion).Wait();
			var result = destinationClient.Synchronization.StartSynchronizationToAsync("test.txt", sourceClient.ServerUrl).Result;
		    Assert.Equal(destinationContent.Length, result.BytesCopied + result.BytesTransfered);

		    // check if conflict resolution has been properly set on the source
		    string resultMd5;
		    using (var resultFileContent = new MemoryStream())
		    {
		        var metadata = sourceClient.DownloadAsync("test.txt", resultFileContent).Result;
		        Assert.Equal("shouldnt-be-overwritten", metadata["SomeTest-metadata"]);
		        resultFileContent.Position = 0;
		        resultMd5 = IOExtensions.GetMD5Hash(resultFileContent);
		        resultFileContent.Position = 0;
		    }

		    destinationContent.Position = 0;
		    var destinationMd5 = IOExtensions.GetMD5Hash(destinationContent);
		    sourceContent.Position = 0;

		    Assert.True(resultMd5 == destinationMd5);
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
					destinationClient.UploadAsync(item, new RandomlyModifiedStream(new RandomStream(1000), 0.01)),
					sourceClient.UploadAsync(item, new RandomStream(1000)));

				RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, item);
			}

			var result = destinationClient.Synchronization.GetFinishedAsync().Result;
			Assert.Equal(files.Length, result.Count());
		}

		[Fact]
		public void Should_refuse_to_synchronize_if_limit_of_concurrent_synchronizations_exceeded()
		{
			var sourceContent = new RandomStream(1);
			var sourceClient = NewClient(1);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationLimit,
			                              new NameValueCollection {{"value", "\"-1\""}}).Wait();

			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			var synchronizationReport = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", "http://localhost:1234").Result;

			Assert.Equal("The limit of active synchronizations to http://localhost:1234 server has been achieved.", synchronizationReport.Exception.Message);
		} 
	}
}
