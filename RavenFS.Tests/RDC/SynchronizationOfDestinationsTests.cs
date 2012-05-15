namespace RavenFS.Tests.RDC
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using Client;
	using Extensions;
	using Newtonsoft.Json;
	using RavenFS.Notifications;
	using Rdc;
	using Tools;
	using Util;
	using Xunit;
	using Xunit.Extensions;

	public class SynchronizationOfDestinationsTests : MultiHostTestBase
	{
		[Fact]
		public void Should_synchronize_to_all_destinations_when_file_uploaded()
		{
			var sourceContent = RdcTestUtils.PrepareSourceStream(10000);
			sourceContent.Position = 0;

			var sourceClient = NewClient(0);

			var destination1Client = NewClient(1);
			var destination2Client = NewClient(2);

			var destination1Content = new RandomlyModifiedStream(sourceContent, 0.01);
			sourceContent.Position = 0;
			var destination2Content = new RandomlyModifiedStream(sourceContent, 0.01);
			sourceContent.Position = 0;

			destination1Client.UploadAsync("test.bin", new NameValueCollection(), destination1Content).Wait();
			destination2Client.UploadAsync("test.bin", new NameValueCollection(), destination2Content).Wait();

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destination1Client.ServerUrl },
																										{ "url", destination2Client.ServerUrl }
			                                                                                     	}).Wait();


			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();
			sourceContent.Position = 0;

			RdcTestUtils.Destinations.WaitForConflict(destination1Client, "test.bin");
			RdcTestUtils.Destinations.WaitForConflict(destination2Client, "test.bin");

			destination1Client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.bin",
			                                                       ConflictResolutionStrategy.RemoteVersion).Wait();
			destination2Client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.bin",
																	ConflictResolutionStrategy.RemoteVersion).Wait();

			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destination2Client, "test.bin");
			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destination1Client, "test.bin");
			
			string destination1Md5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				destination1Client.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				destination1Md5 = resultFileContent.GetMD5Hash();
			}

			string destination2Md5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				destination2Client.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				destination2Md5 = resultFileContent.GetMD5Hash();
			}

			sourceContent.Position = 0;
			var sourceMd5 = sourceContent.GetMD5Hash();

			Assert.Equal(sourceMd5, destination1Md5);
			Assert.Equal(sourceMd5, destination2Md5);
			Assert.Equal(destination1Md5, destination2Md5);
		}

		[Fact]
		public void When_destination_is_down_next_file_upload_should_synchronize_missing_files()
		{
			var sourceClient = NewClient(0);

			var source1Content = new RandomStream(10000);

			sourceClient.UploadAsync("test1.bin", new NameValueCollection(), source1Content).Wait();

			var destinationClient = NewClient(1);
			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();
			var source2Content = new RandomStream(10000);

			sourceClient.UploadAsync("test2.bin", new NameValueCollection(), source2Content).Wait();

			// TODO not sure why this line causes infitite loop
			//RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destinationClient, "test1.bin");
			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destinationClient, "test2.bin");

			var destinationFiles = destinationClient.GetFilesAsync("/").Result;
			Assert.Equal(2, destinationFiles.FileCount);
			Assert.Equal(2, destinationFiles.Files.Length);
			Assert.NotEqual(destinationFiles.Files[0].Name, destinationFiles.Files[1].Name);
			Assert.True(destinationFiles.Files[0].Name == "test1.bin" || destinationFiles.Files[0].Name == "test2.bin");
			Assert.True(destinationFiles.Files[1].Name == "test1.bin" || destinationFiles.Files[1].Name == "test2.bin");
		}

		[Fact]
		public void Source_should_save_configuration_record_after_synchronization()
		{
			var sourceClient = NewClient(0);
			var sourceContent = new RandomStream(10000);

			var destinationClient = NewClient(1);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();
			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destinationClient, "test.bin");

			var savedRecord =
				sourceClient.Config.GetConfig(SynchronizationHelper.SyncNameForFile("test.bin", destinationClient.ServerUrl)).Result
					["value"];

			var synchronizationDetails = new TypeHidingJsonSerializer().Parse<SynchronizationDetails>(savedRecord);

			Assert.Equal("test.bin", synchronizationDetails.FileName);
			Assert.Equal(destinationClient.ServerUrl, synchronizationDetails.DestinationUrl);
		}

		[Fact]
		public void Source_should_delete_configuration_record_if_destination_confirm_that_file_is_safe()
		{
			var sourceClient = NewClient(0);
			var sourceContent = new RandomStream(10000);

			var destinationClient = NewClient(1);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();
			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destinationClient, "test.bin");

			sourceClient.UploadAsync("test2.bin", new NameValueCollection(), sourceContent).Wait();

			RdcTestUtils.Destinations.WaitForSynchronizationFinishOnDestination(destinationClient, "test2.bin");

			var shouldBeNull =
				sourceClient.Config.GetConfig(SynchronizationHelper.SyncNameForFile("test.bin", destinationClient.ServerUrl)).Result;

			Assert.Null(shouldBeNull);
		}

		[Fact(Skip = "Answer needed")]
		public void File_should_be_in_pending_queue_if_no_synchronization_requests_available()
		{
			var sourceContent = new RandomStream(1);
			var sourceClient = NewClient(0);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationLimit,
										  new NameValueCollection { { "value", "\"-1\"" } }).Wait();

			var destinationClient = NewClient(1);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();

			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			IEnumerable<SynchronizationDetails> pedingSynchronizations = null;
			do
			{
				pedingSynchronizations = sourceClient.Synchronization.GetPendingAsync().Result;
				Thread.Sleep(100);
			} while (pedingSynchronizations.Count() == 0);

			Assert.Equal(1, pedingSynchronizations.Count());
			Assert.Equal("test.bin", pedingSynchronizations.ToArray()[0].FileName);
			Assert.Equal(destinationClient.ServerUrl, pedingSynchronizations.ToArray()[0].DestinationUrl);
		}

		[Fact]
		public void Should_synchronize_to_all_destinations_when_file_metadata_changed()
		{
			// TODO
			// what about: metadata changing, file renaming and deleting? how to synchronize then?
		}

		[Fact]
		public void Should_confirm_that_file_is_safe()
		{
			var sourceContent = new RandomStream(1024 * 1024);

			var sourceClient = NewClient(1);
			var destinationClient = NewClient(0);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();
			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();
			sourceClient.Synchronization.SynchronizeDestinationsAsync();

			SynchronizationReport report;
			do
			{
				report = destinationClient.Synchronization.GetSynchronizationStatusAsync("test.bin").Result;
			} while (report == null);

			var confirmations = destinationClient.Synchronization.ConfirmFilesAsync(new List<string> {"test.bin"}).Result;

			Assert.Equal(1, confirmations.Count());
			Assert.Equal(FileStatus.Safe, confirmations.ToArray()[0].Status);
			Assert.Equal("test.bin", confirmations.ToArray()[0].FileName);
		}

		[Fact]
		public void Should_report_that_file_state_is_unknown_if_file_doesnt_exist()
		{
			var destinationClient = NewClient(0);

			var confirmations = destinationClient.Synchronization.ConfirmFilesAsync(new List<string> { "test.bin" }).Result;

			Assert.Equal(1, confirmations.Count());
			Assert.Equal(FileStatus.Unknown, confirmations.ToArray()[0].Status);
			Assert.Equal("test.bin", confirmations.ToArray()[0].FileName);
		}
		
		[Fact]
		public void Should_report_that_file_is_broken_if_last_synchronization_set_exception()
		{
			var destinationClient = NewClient(0);

			var failureSynchronization = new SynchronizationReport()
			                             	{Exception = new Exception("There was an exception in last synchronization.")};

			var sb = new StringBuilder();
            var jw = new JsonTextWriter(new StringWriter(sb));
            new JsonSerializer().Serialize(jw, failureSynchronization);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncResultNameForFile("test.bin"),
			                                   new NameValueCollection() {{"value", sb.ToString()}}).Wait();

			var confirmations = destinationClient.Synchronization.ConfirmFilesAsync(new List<string> { "test.bin" }).Result;

			Assert.Equal(1, confirmations.Count());
			Assert.Equal(FileStatus.Broken, confirmations.ToArray()[0].Status);
			Assert.Equal("test.bin", confirmations.ToArray()[0].FileName);
		}
	}
}