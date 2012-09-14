
namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Threading.Tasks;
	using IO;
	using Newtonsoft.Json;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Synchronization;
	using RavenFS.Util;
	using Xunit;
	using Xunit.Extensions;

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
			destinationContent.Position = 0;
			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);
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
				resultMd5 = resultFileContent.GetMD5Hash();
				resultFileContent.Position = 0;
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

			Assert.True(resultMd5 == sourceMd5);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(5000)]
		public void Synchronize_file_with_appended_data(int size)
		{
			var differenceChunk = new MemoryStream();
			var sw = new StreamWriter(differenceChunk);

			sw.Write("Coconut is Stupid");
			sw.Flush();

			var sourceContent = new CombinedStream(SyncTestUtils.PrepareSourceStream(size), differenceChunk);
			sourceContent.Position = 0;
			var destinationContent = SyncTestUtils.PrepareSourceStream(size);
			destinationContent.Position = 0;
			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			destinationClient.UploadAsync("test.txt", destinationContent).Wait();
			sourceContent.Position = 0;
			sourceClient.UploadAsync("test.txt", sourceContent).Wait();

			SynchronizationReport result = SyncTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.txt");

			Assert.Equal(sourceContent.Length, result.BytesCopied + result.BytesTransfered);

			string resultMd5;
			using (var resultFileContent = new MemoryStream())
			{
				destinationClient.DownloadAsync("test.txt", resultFileContent).Wait();
				resultFileContent.Position = 0;
				resultMd5 = resultFileContent.GetMD5Hash();
				resultFileContent.Position = 0;
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

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
				resultMd5 = resultFileContent.GetMD5Hash();
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

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
			Assert.Equal("No synchronization work needed. Destination server had this file in the past.", secondSynchronization.Exception.Message);
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
		public void Should_get_all_finished_synchronizations()
		{
			var destinationClient = NewClient(0);
			var sourceClient = NewClient(1);
			var files = new[] { "test1.bin", "test2.bin", "test3.bin" };

			// make sure that returns empty list if there are no finished synchronizations yet
			var result = destinationClient.Synchronization.GetFinishedAsync().Result;
			Assert.Equal(0, result.TotalCount);

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

			result = destinationClient.Synchronization.GetFinishedAsync().Result;
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

			Assert.Equal("The limit of active synchronizations to " + destinationClient.ServerUrl + " server has been achieved. Cannot process a file 'test.bin'.", synchronizationReport.Exception.Message);
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
			var buffer = new byte[1024 * 1024 * 5 + 10];
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
		public void Empty_file_should_be_synchronized_correctly()
		{
			var source = NewClient(0);
			var destination = NewClient(1);

			source.UploadAsync("empty.test", new NameValueCollection() { {"should-be-transferred", "true"} }, new MemoryStream()).Wait();
			var result = source.Synchronization.StartSynchronizationToAsync("empty.test", destination.ServerUrl).Result;

			Assert.Null(result.Exception);

			using (var ms = new MemoryStream())
			{
				var metadata = destination.DownloadAsync("empty.test", ms).Result;

				Assert.Equal("true", metadata["should-be-transferred"]);
				Assert.Equal(0, ms.Length);
			}
		}

		[Fact]
		public void Should_throw_exception_if_synchronized_file_doesnt_exist()
		{
			var source = NewClient(0);
			var destination = NewClient(1);

			var result = source.Synchronization.StartSynchronizationToAsync("file_which_doesnt_exist", destination.ServerUrl).Result;

			Assert.Equal("File does not exist locally", result.Exception.Message);
		}

		[Fact]
		public void Can_increment_last_etag()
		{
			var client = NewClient(1);

			var id = Guid.NewGuid();
			var etag = Guid.NewGuid();

			client.Synchronization.IncrementLastETagAsync(id, etag).Wait();

			var lastSyncInfo = client.Synchronization.GetLastSynchronizationFromAsync(id).Result;

			Assert.Equal(etag, lastSyncInfo.LastSourceFileEtag);
		}

		[Fact]
		public void Can_synchronize_file_with_greater_number_of_signatures()
		{
			const int size5Mb = 1024 * 1024 * 5;
			const int size1Mb = 1024 * 1024;

			var source = NewClient(0);
			var destination = NewClient(1);

			var buffer = new byte[size5Mb]; // 5Mb file should have 2 signatures
            new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			source.UploadAsync("test.bin", sourceContent).Wait();

			buffer = new byte[size1Mb]; // while 1Mb file has only 1 signature
            new Random().NextBytes(buffer);

			destination.UploadAsync("test.bin", new MemoryStream(buffer)).Wait();

			var sourceSigCount = source.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;
			var destinationSigCount = destination.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;

			// ensure that file on source has more signatures than file on destination
			Assert.True(sourceSigCount > destinationSigCount, "File on source should be much bigger in order to have more signatures");

			var result = SyncTestUtils.ResolveConflictAndSynchronize(source, destination, "test.bin");

			Assert.Null(result.Exception);
			Assert.Equal(size5Mb, result.BytesTransfered + result.BytesCopied);
			sourceContent.Position = 0;
			Assert.Equal(sourceContent.GetMD5Hash(), destination.GetMetadataForAsync("test.bin").Result["Content-MD5"]);
		}

		[Fact]
		public void Can_synchronize_file_with_less_number_of_signatures()
		{
			const int size5Mb = 1024 * 1024 * 5;
			const int size1Mb = 1024 * 1024;

			var source = NewClient(0);
			var destination = NewClient(1);

			var buffer = new byte[size1Mb]; // 1Mb file should have 1 signature
			new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			source.UploadAsync("test.bin", sourceContent).Wait();

			buffer = new byte[size5Mb]; // while 5Mb file has 2 signatures
			new Random().NextBytes(buffer);

			destination.UploadAsync("test.bin", new MemoryStream(buffer)).Wait();

			var sourceSigCount = source.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;
			var destinationSigCount = destination.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;

			Assert.True(sourceSigCount > 0, "Source file should have one signature");
			// ensure that file on source has less signatures than file on destination
			Assert.True(sourceSigCount < destinationSigCount, "File on source should be smaller in order to have less signatures");

			var result = SyncTestUtils.ResolveConflictAndSynchronize(source, destination, "test.bin");

			Assert.Null(result.Exception);
			Assert.Equal(size1Mb, result.BytesTransfered + result.BytesCopied);
			sourceContent.Position = 0;
			Assert.Equal(sourceContent.GetMD5Hash(), destination.GetMetadataForAsync("test.bin").Result["Content-MD5"]);
		}

		[Fact]
		public void Can_synchronize_file_that_doesnt_have_any_signature_while_file_on_destination_has()
		{
			const int size1b = 1;
			const int size5Mb = 1024 * 1024 * 5;

			var source = NewClient(0);
			var destination = NewClient(1);

			var buffer = new byte[size1b]; // 1b file should have no signatures
			new Random().NextBytes(buffer);

			var sourceContent = new MemoryStream(buffer);
			source.UploadAsync("test.bin", sourceContent).Wait();

			buffer = new byte[size5Mb]; // 5Mb file should have 2 signatures
			new Random().NextBytes(buffer);

			destination.UploadAsync("test.bin", new MemoryStream(buffer)).Wait();

			var sourceSigCount = source.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;
			var destinationSigCount = destination.Synchronization.GetRdcManifestAsync("test.bin").Result.Signatures.Count;

			Assert.Equal(0, sourceSigCount); // ensure that file on source has no signature
			Assert.True(destinationSigCount > 0, "File on destination should have any signature");

			var result = SyncTestUtils.ResolveConflictAndSynchronize(source, destination, "test.bin");

			Assert.Null(result.Exception);
			Assert.Equal(size1b, result.BytesTransfered);
			sourceContent.Position = 0;
			Assert.Equal(sourceContent.GetMD5Hash(), destination.GetMetadataForAsync("test.bin").Result["Content-MD5"]);
		}
	}
}