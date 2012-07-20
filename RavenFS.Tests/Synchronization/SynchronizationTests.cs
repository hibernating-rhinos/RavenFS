namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using IO;
	using Newtonsoft.Json;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Notifications;
	using RavenFS.Synchronization;
	using RavenFS.Synchronization.Conflictuality;
	using RavenFS.Synchronization.Multipart;
	using RavenFS.Util;
	using Xunit;
	using Xunit.Extensions;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;
	using RavenFS.Tests.Tools;

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

			var sourceContent = SyncTestUtils.PrepareSourceStream(size);
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

			SynchronizationReport result = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.txt");

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
			var sourceContent = SyncTestUtils.PrepareSourceStream(size);
			sourceContent.Position = 0;
			var destinationContent = new RandomlyModifiedStream(sourceContent, 0.01);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			destinationClient.UploadAsync("test.txt", new NameValueCollection(), destinationContent).Wait();
			sourceContent.Position = 0;
			sourceClient.UploadAsync("test.txt", new NameValueCollection(), sourceContent).Wait();

			SynchronizationReport result = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.txt");

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
		[InlineData(1024 * 1024)]
		public void Synchronization_of_already_synchronized_file_should_detect_that_no_work_is_needed(int size)
		{
			var r = new Random(1);

			var bytes = new byte[size];

			r.NextBytes(bytes);

			var sourceContent = new MemoryStream(bytes);
			var destinationContent = new RandomlyModifiedStream(new RandomStream(size, 1), 0.01, 1);
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);

			destinationClient.UploadAsync("test.bin", new NameValueCollection(), destinationContent).Wait();
			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			var firstSynchronization = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

			Assert.Equal(sourceContent.Length, firstSynchronization.BytesCopied + firstSynchronization.BytesTransfered);

			string resultMd5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				destinationClient.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				resultMd5 = resultFileContent.GetMD5Hash();
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

			Assert.Equal(sourceMd5, resultMd5);

			var secondSynchronization = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			using (var resultFileContent = new MemoryStream())
			{
				destinationClient.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				resultMd5 = resultFileContent.GetMD5Hash();
			}

			sourceContent.Position = 0;
			sourceMd5 = sourceContent.GetMD5Hash();

			Assert.Equal(sourceMd5, resultMd5);

			Assert.Equal(0, secondSynchronization.NeedListLength);
			Assert.Equal(0, secondSynchronization.BytesTransfered);
			Assert.Equal(0, secondSynchronization.BytesCopied);
			Assert.Equal("No synchronization work needed", secondSynchronization.Exception.Message);
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

			SynchronizationReport result = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");
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

			SynchronizationReport result = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");
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

			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync(sourceClient.GetServerId().Result).Result;

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
			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync(sourceClient.GetServerId().Result).Result;

			Assert.Equal(lastSourceETag, lastSynchronization.LastSourceFileEtag);
		}

		[Fact]
		public void Destination_should_return_empty_guid_as_last_etag_if_no_syncing_was_made()
		{
			var destinationClient = NewClient(0);

			var lastSynchronization = destinationClient.Synchronization.GetLastSynchronizationFromAsync(Guid.Empty).Result;

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
		public void Should_be_possible_to_apply_conflict()
		{
			var content = new RandomStream(10);
			var client = NewClient(1);
			client.UploadAsync("test.bin", content).Wait();
			var guid = Guid.NewGuid().ToString();
			client.Synchronization.ApplyConflictAsync("test.bin", 8, guid).Wait();
			var resultFileMetadata = client.GetMetadataForAsync("test.bin").Result;
			var conflictItemString = client.Config.GetConfig(SynchronizationHelper.ConflictConfigNameForFile("test.bin")).Result["value"];
			var conflict = new TypeHidingJsonSerializer().Parse<ConflictItem>(conflictItemString);

			Assert.Equal(true.ToString(), resultFileMetadata[SynchronizationConstants.RavenSynchronizationConflict]);
			Assert.Equal(guid, conflict.Remote.ServerId);
			Assert.Equal(8, conflict.Remote.Version);
			Assert.Equal(1, conflict.Current.Version);
		}

		[Fact]
		public void Should_throw_not_found_exception_when_applying_conflict_on_missing_file()
		{
			var client = NewClient(1);

			var guid = Guid.NewGuid().ToString();
			var innerException = SyncTestUtils.ExecuteAndGetInnerException(() => client.Synchronization.ApplyConflictAsync("test.bin", 8, guid).Wait());

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
			Assert.True(Convert.ToBoolean(resultFileMetadata[SynchronizationConstants.RavenSynchronizationConflict]));
		}

		[Fact]
		public void Must_not_synchronize_conflicted_file()
		{
			var sourceContent = new RandomStream(10);
			var sourceMetadataWithConflict = new NameValueCollection
		                       {
		                           {SynchronizationConstants.RavenSynchronizationConflict, "true"}
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
			sourceClient.UploadAsync("test.bin", sourceContent1).Wait();
			var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenSynchronizationHistory];
			var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

			Assert.Equal(0, history.Count);

			sourceClient.UploadAsync("test.bin", sourceContent1).Wait();
			historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenSynchronizationHistory];
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
			var historySerialized = sourceClient.GetMetadataForAsync("test.bin").Result[SynchronizationConstants.RavenSynchronizationHistory];
			var history = new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(historySerialized)));

			Assert.Equal(0, history.Count);

			sourceClient.UpdateMetadataAsync("test.bin", new NameValueCollection { { "test", "Changed" } }).Wait();
			var metadata = sourceClient.GetMetadataForAsync("test.bin").Result;
			historySerialized = metadata[SynchronizationConstants.RavenSynchronizationHistory];
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

			SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

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

		    var sourceContent = SyncTestUtils.PrepareSourceStream(10);
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

			Assert.Equal("File test.txt is conflicted", shouldBeConflict.Exception.Message);

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
				var sourceContent = new MemoryStream();
				var sw = new StreamWriter(sourceContent);

				sw.Write("abc123");
				sw.Flush();

				sourceContent.Position = 0;

				var destinationContent = new MemoryStream();
				var sw2 = new StreamWriter(destinationContent);

				sw2.Write("cba321");
				sw2.Flush();

				destinationContent.Position = 0;

				Task.WaitAll(
					destinationClient.UploadAsync(item, destinationContent),
					sourceClient.UploadAsync(item,  sourceContent));

				SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, item);
			}

			var result = destinationClient.Synchronization.GetFinishedAsync().Result;
			Assert.Equal(files.Length, result.TotalCount);
		}

		[Fact]
		public void Should_refuse_to_synchronize_if_limit_of_concurrent_synchronizations_exceeded()
		{
			var sourceContent = new RandomStream(1);
			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenSynchronizationLimit,
			                              new NameValueCollection {{"value", "\"-1\""}}).Wait();

			sourceClient.UploadAsync("test.bin", sourceContent).Wait();

			var synchronizationReport = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.Equal("The limit of active synchronizations to " + destinationClient.ServerUrl + " server has been achieved.", synchronizationReport.Exception.Message);
		}

		[Fact]
		public void Should_calculate_and_save_content_hash_after_upload()
		{
			var buffer = new byte[1024];
			new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			var sourceClient = NewClient(0);

			sourceClient.UploadAsync("test.bin", sourceContent).Wait();
			sourceContent.Position = 0;
			var resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;

			Assert.Contains("Content-MD5", resultFileMetadata.AllKeys);
			Assert.Equal(sourceContent.GetMD5Hash(), resultFileMetadata["Content-MD5"]);
		}

		[Fact]
		public void Should_calculate_and_save_content_hash_after_synchronization()
		{
			var buffer = new byte[1024*1024*5];
			new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			var sourceClient = NewClient(0);

			sourceClient.UploadAsync("test.bin", sourceContent).Wait();
			sourceContent.Position = 0;

			var destinationClient = NewClient(1);
			destinationClient.UploadAsync("test.bin", new RandomlyModifiedStream(sourceContent, 0.01)).Wait();
			sourceContent.Position = 0;

			SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin"); 
			var resultFileMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;
			
			Assert.Contains("Content-MD5", resultFileMetadata.AllKeys);
			Assert.Equal(sourceContent.GetMD5Hash(), resultFileMetadata["Content-MD5"]);
		}

		[Fact]
		public void Should_not_change_content_hash_after_metadata_upload()
		{
			var buffer = new byte[1024];
			new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			var sourceClient = NewClient(0);

			sourceClient.UploadAsync("test.bin", sourceContent).Wait();
			sourceClient.UpdateMetadataAsync("test.bin", new NameValueCollection() { { "someKey", "someValue" } }).Wait();

			sourceContent.Position = 0;
			var resultFileMetadata = sourceClient.GetMetadataForAsync("test.bin").Result;

			Assert.Contains("Content-MD5", resultFileMetadata.AllKeys);
			Assert.Equal(sourceContent.GetMD5Hash(), resultFileMetadata["Content-MD5"]);
		}

		[Fact]
		public void Should_synchronize_just_metadata()
		{
			var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });

			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new NameValueCollection { { "difference", "metadata" } }, content).Wait();
			content.Position = 0;
			destinationClient.UploadAsync("test.bin", content).Wait();

			var report = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

			Assert.Equal(SynchronizationType.MetadataUpdate, report.Type);

			var destinationMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;

			Assert.Equal("metadata",destinationMetadata["difference"]);
		}

		[Fact]
		public void Should_detect_conflict_on_metadata_synchronization()
		{
			var content = new MemoryStream(new byte[] {1, 2, 3, 4});

			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new NameValueCollection {{"difference", "metadata"}}, content).Wait();
			content.Position = 0;
			destinationClient.UploadAsync("test.bin", content).Wait();

			var report = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.Equal(SynchronizationType.MetadataUpdate, report.Type);
			Assert.Equal("File test.bin is conflicted", report.Exception.Message);
		}

		[Fact]
		public void Should_just_rename_file_in_synchronization_process()
		{
			var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });

			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new NameValueCollection { { "key", "value" } }, content).Wait();
			content.Position = 0;
			destinationClient.UploadAsync("test.bin", new NameValueCollection { { "key", "value" } }, content).Wait();

			sourceClient.RenameAsync("test.bin", "renamed.bin").Wait();

			// we need to indicate old file name, otherwise content update would be performed because renamed file does not exist on dest
			var report = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

			Assert.Equal(SynchronizationType.Rename, report.Type);

			var testMetadata = destinationClient.GetMetadataForAsync("test.bin").Result;
			var renamedMetadata = destinationClient.GetMetadataForAsync("renamed.bin").Result;

			Assert.Null(testMetadata);
			Assert.NotNull(renamedMetadata);

			var result = destinationClient.GetFilesAsync("/").Result;

			Assert.Equal(1, result.FileCount);
			Assert.Equal("renamed.bin", result.Files[0].Name);
		}

		[Fact]
		public void Should_detect_conflict_on_renaming_synchronization()
		{
			var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });

			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			sourceClient.UploadAsync("test.bin", new NameValueCollection { { "key", "value" } }, content).Wait();
			content.Position = 0;
			destinationClient.UploadAsync("test.bin", new NameValueCollection { { "key", "value" } }, content).Wait();

			sourceClient.RenameAsync("test.bin", "renamed.bin").Wait();

			// we need to indicate old file name, otherwise content update would be performed because renamed file does not exist on dest
			var report = sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl).Result;

			Assert.Equal(SynchronizationType.Rename, report.Type);
			Assert.Equal("File test.bin is conflicted", report.Exception.Message);
		}

		[Fact]
		public void Should_successfully_get_finished_and_conflicted_synchronization()
		{
			var destinationClient = NewClient(1);

			destinationClient.UploadAsync("test.bin", new NameValueCollection { { "key", "value" } }, new MemoryStream(new byte[] { 1, 2, 3, 4 })).Wait();

			var webRequest =
				(HttpWebRequest) WebRequest.Create(destinationClient.ServerUrl + "/synchronization/updatemetadata/test.bin");
			webRequest.ContentLength = 0;
			webRequest.Method = "POST";

			webRequest.Headers.Add(SyncingMultipartConstants.SourceServerId, Guid.Empty.ToString());
			webRequest.Headers.Add("ETag", "\"" + new Guid() + "\"");
			webRequest.Headers.Add("MetadataKey", "MetadataValue");

			var sb = new StringBuilder();
			new JsonSerializer().Serialize(new JsonTextWriter(new StringWriter(sb)),
			                               new List<HistoryItem>
			                               	{
			                               		new HistoryItem
			                               			{
			                               				ServerId = new Guid().ToString(),
			                               				Version = 1
			                               			}
			                               	});
			
			webRequest.Headers.Add(SynchronizationConstants.RavenSynchronizationHistory, sb.ToString());
			webRequest.Headers.Add(SynchronizationConstants.RavenSynchronizationVersion, "1");

			var httpWebResponse = webRequest.MakeRequest();
			Assert.Equal(HttpStatusCode.OK, httpWebResponse.StatusCode);

			var finishedSynchronizations = destinationClient.Synchronization.GetFinishedAsync().Result.Items;

			Assert.Equal(1, finishedSynchronizations.Count);
			Assert.Equal("test.bin", finishedSynchronizations[0].FileName);
			Assert.Equal(SynchronizationType.MetadataUpdate, finishedSynchronizations[0].Type);
			Assert.Equal("File test.bin is conflicted", finishedSynchronizations[0].Exception.Message);
		}
	}
}
