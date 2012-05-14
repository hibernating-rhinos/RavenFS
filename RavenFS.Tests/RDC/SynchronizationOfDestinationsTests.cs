namespace RavenFS.Tests.RDC
{
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using Client;
	using Extensions;
	using Rdc;
	using Tools;
	using Xunit;
	using Xunit.Extensions;

	public class SynchronizationOfDestinationsTests : MultiHostTestBase
	{
		[Fact(Timeout = 60000)]
		public void Should_synchronize_to_all_destinations_when_file_uploaded()
		{
			var sourceContent = RdcTestUtils.PrepareSourceStream(1024);
			sourceContent.Position = 0;

			var sourceClient = NewClient(0);

			var destination1Client = NewClient(1);
			var destination2Client = NewClient(2);

			var destination1Content = new RandomlyModifiedStream(new RandomStream(1024, 1), 0.01);
			var destination2Content = new RandomlyModifiedStream(new RandomStream(1024, 1), 0.01);

			destination1Client.UploadAsync("test.bin", new NameValueCollection(), destination1Content).Wait();
			destination2Client.UploadAsync("test.bin", new NameValueCollection(), destination2Content).Wait();

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destination1Client.ServerUrl },
																										{ "url", destination2Client.ServerUrl }
			                                                                                     	}).Wait();


			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();

			RdcTestUtils.Destinations.WaitForConflict(destination1Client, "test.bin");
			RdcTestUtils.Destinations.WaitForConflict(destination2Client, "test.bin");

			destination1Client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.bin",
			                                                        ConflictResolutionStrategy.RemoteVersion).Wait();
			destination2Client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, "test.bin",
																	ConflictResolutionStrategy.RemoteVersion).Wait();

			sourceContent.Position = 0;
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
		public void Should_synchronize_to_all_destinations_when_file_metadata_changed()
		{

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
		public void Should_confirm_that_file_state_is_unknown()
		{
			var destinationClient = NewClient(0);

			var confirmations = destinationClient.Synchronization.ConfirmFilesAsync(new List<string> { "test.bin" }).Result;

			Assert.Equal(1, confirmations.Count());
			Assert.Equal(FileStatus.Unknown, confirmations.ToArray()[0].Status);
			Assert.Equal("test.bin", confirmations.ToArray()[0].FileName);
		}
	}
}