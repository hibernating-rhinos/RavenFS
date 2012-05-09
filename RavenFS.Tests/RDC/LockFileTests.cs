using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using RavenFS.Client;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Util;
using Xunit;

namespace RavenFS.Tests.RDC
{
	using Rdc;

	public class LockFileTests : MultiHostTestBase
	{
		private const int RetriesCount = 300;
		private readonly NameValueCollection EmptyData = new NameValueCollection();

		[Fact(Skip = "This test is actually relying on a race condition to run, if the sync finished before the Wait starts, it will fail")]
		public void Should_create_sync_configuration_during_synchronization()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			var configName = SynchronizationHelper.SyncNameForFile("test.bin");

			sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl);

			Assert.True(WaitForBeginningSynchronization(destinationClient, configName));
		}

		[Fact]
		public void Should_delete_sync_configuration_after_synchronization()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			sourceClient.Synchronization.StartSynchronizationToAsync("test.bin", destinationClient.ServerUrl);
			var config = destinationClient.Config.GetConfig(SynchronizationHelper.SyncNameForFile("test.bin")).Result;

			Assert.Null(config);
		}

		[Fact]
		public void Should_refuse_to_update_metadata_while_sync_configuration_exists()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var innerException = RdcTestUtils.ExecuteAndGetInnerException(() => destinationClient.UpdateMetadataAsync("test.bin", new NameValueCollection()).Wait());

			Assert.IsType(typeof(SynchronizationException), innerException);
			Assert.Equal("File test.bin is being synced", innerException.Message);
		}

		[Fact]
		public void Should_refuse_to_delete_file_while_sync_configuration_exists()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var innerException = RdcTestUtils.ExecuteAndGetInnerException(() => destinationClient.DeleteAsync("test.bin").Wait());

			Assert.IsType(typeof(SynchronizationException), innerException);
			Assert.Equal("File test.bin is being synced", innerException.Message);
		}

		[Fact]
		public void Should_refuse_to_rename_file_while_sync_configuration_exists()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var innerException = RdcTestUtils.ExecuteAndGetInnerException(() => destinationClient.RenameAsync("test.bin", "newname.bin").Wait());

			Assert.IsType(typeof(SynchronizationException), innerException);
			Assert.Equal("File test.bin is being synced", innerException.Message);
		}

		[Fact]
		public void Should_refuse_to_upload_file_while_sync_configuration_exists()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var innerException = RdcTestUtils.ExecuteAndGetInnerException(() => destinationClient.UploadAsync("test.bin", EmptyData, new MemoryStream()).Wait());

			Assert.IsType(typeof(SynchronizationException), innerException);
			Assert.Equal("File test.bin is being synced", innerException.Message);
		}

		[Fact]
		public void Should_refuse_to_synchronize_file_while_sync_configuration_exists()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var synchronizationReport = RdcTestUtils.ResolveConflictAndSynchronize(sourceClient, destinationClient, "test.bin");

			Assert.Equal("File test.bin is being synced", synchronizationReport.Exception.Message);
		}

		[Fact]
		public void Should_successfully_update_metadata_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			ZeroTimeoutTest(destinationClient, () => destinationClient.UpdateMetadataAsync("test.bin", new NameValueCollection()).Wait());
		}

		[Fact]
		public void Should_successfully_delete_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			ZeroTimeoutTest(destinationClient, () => destinationClient.DeleteAsync("test.bin").Wait());
		}

		[Fact]
		public void Should_successfully_rename_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			ZeroTimeoutTest(destinationClient, () => destinationClient.RenameAsync("test.bin", "newname.bin").Wait());
		}

		[Fact]
		public void Should_successfully_upload_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			ZeroTimeoutTest(destinationClient, () => destinationClient.UploadAsync("test.bin", EmptyData, new MemoryStream()).Wait());
		}

		[Fact]
		public void Should_successfully_synchronize_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient destinationClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out destinationClient);

			destinationClient.Config.SetConfig(SynchronizationConstants.RavenReplicationTimeout, new NameValueCollection()
                                                                                        {
                                                                                            {"value", "\"00:00:00\""}
                                                                                        }).Wait();

			Assert.DoesNotThrow(() => RdcTestUtils.ResolveConflictAndSynchronize(sourceClient,
			                                                                     destinationClient,
			                                                                     "test.bin"));
		}

		private void UploadFilesSynchronously(out RavenFileSystemClient sourceClient, out RavenFileSystemClient destinationClient, string fileName = "test.bin")
		{
			sourceClient = NewClient(1);
			destinationClient = NewClient(0);

			var sourceContent = new RandomlyModifiedStream(new RandomStream(10, 1), 0.01);
			var destinationContent = new RandomlyModifiedStream(new RandomStream(10, 1), 0.01);

			destinationClient.UploadAsync(fileName, EmptyData, destinationContent).Wait();
			sourceClient.UploadAsync(fileName, EmptyData, sourceContent).Wait();
		}

		

		private static bool WaitForBeginningSynchronization(RavenFileSystemClient client, string configName)
		{
			for (int i = 0; i < RetriesCount; i++)
			{
				var configNames = client.Config.GetConfigNames().Result;
				if (configNames.Any(t => t == configName))
				{
					return true;
				}

				Thread.Sleep(50);
			}

			return false;
		}

		private static NameValueCollection SynchronizationConfig(DateTime fileLockedDate)
		{
			return new NameValueCollection()
                                    {
                                        {"value", "{\"SourceUrl\":\"http://localhost:19081\",\"FileLockedAt\":\"\\/Date(" + fileLockedDate.Ticks + ")\\/\"}"},
                                    };
		}

		private static void ZeroTimeoutTest(RavenFileSystemClient destinationClient, Action action)
		{
			destinationClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.MinValue)).Wait();

			destinationClient.Config.SetConfig(SynchronizationConstants.RavenReplicationTimeout, new NameValueCollection()
                                                                                        {
                                                                                            {"value", "\"00:00:00\""}
                                                                                        }).Wait();

			Assert.DoesNotThrow(() => action());
		}
	}
}
