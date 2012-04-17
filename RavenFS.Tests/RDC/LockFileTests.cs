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
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

            var configName = SynchronizationHelper.SyncNameForFile("test.bin");

            seedClient.Synchronization.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin");

            Assert.True(WaitForBeginningSynchronization(seedClient, configName));
        }

        [Fact]
        public void Should_delete_sync_configuration_after_synchronization()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

            RdcTestUtils.SynchronizeAndWaitForStatus(seedClient, sourceClient.ServerUrl, "test.bin");
            var config = seedClient.Config.GetConfig(SynchronizationHelper.SyncNameForFile("test.bin")).Result;

            Assert.Null(config);
        }

        [Fact]
        public void Should_refuse_to_update_metadata_while_sync_configuration_exists()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

            seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

            var innerException = ExecuteAndGetInnerException(() => seedClient.UpdateMetadataAsync("test.bin", new NameValueCollection()).Wait());

            Assert.IsType(typeof(InvalidOperationException), innerException);
            Assert.Contains("File test.bin is being synced", innerException.Message);
        }

		[Fact]
        public void Should_refuse_to_delete_file_while_sync_configuration_exists()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

			seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

            var innerException = ExecuteAndGetInnerException(() => seedClient.DeleteAsync("test.bin").Wait());

            Assert.IsType(typeof(InvalidOperationException), innerException);
            Assert.Contains("File test.bin is being synced", innerException.Message);
        }

        [Fact]
        public void Should_refuse_to_rename_file_while_sync_configuration_exists()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

			seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

            var innerException = ExecuteAndGetInnerException(() => seedClient.RenameAsync("test.bin", "newname.bin").Wait());

            Assert.IsType(typeof(InvalidOperationException), innerException);
            Assert.Contains("File test.bin is being synced", innerException.Message);
        }

        [Fact]
        public void Should_refuse_to_upload_file_while_sync_configuration_exists()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

			seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

			var innerException = ExecuteAndGetInnerException(() => seedClient.UploadAsync("test.bin", EmptyData, new MemoryStream()).Wait());

            Assert.IsType(typeof(InvalidOperationException), innerException);
            Assert.Contains("File test.bin is being synced", innerException.Message);
        }

        [Fact]
        public void Should_refuse_to_synchronize_file_while_sync_configuration_exists()
        {
            RavenFileSystemClient seedClient;
            RavenFileSystemClient sourceClient;

            UploadFilesSynchronously(out sourceClient, out seedClient);

			seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.UtcNow)).Wait();

            var innerException = ExecuteAndGetInnerException(() => seedClient.Synchronization.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin").Wait());

            Assert.IsType(typeof(InvalidOperationException), innerException);
            Assert.Contains("File test.bin is being synced", innerException.Message);
        }

		[Fact]
		public void Should_successfully_update_metadata_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient seedClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out seedClient);

			ZeroTimeoutTest(seedClient, () => seedClient.UpdateMetadataAsync("test.bin", new NameValueCollection()).Wait());
		}

		[Fact]
		public void Should_successfully_delete_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient seedClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out seedClient);

			ZeroTimeoutTest(seedClient, () => seedClient.DeleteAsync("test.bin").Wait());
		}

		[Fact]
		public void Should_successfully_rename_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient seedClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out seedClient);

			ZeroTimeoutTest(seedClient, () => seedClient.RenameAsync("test.bin", "newname.bin").Wait());
		}

		[Fact]
		public void Should_successfully_upload_file_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient seedClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out seedClient);

			ZeroTimeoutTest(seedClient, () => seedClient.UploadAsync("test.bin", EmptyData, new MemoryStream()).Wait());
		}

		[Fact]
		public void Should_successfully_synchronize_if_last_synchronization_timeout_exceeded()
		{
			RavenFileSystemClient seedClient;
			RavenFileSystemClient sourceClient;

			UploadFilesSynchronously(out sourceClient, out seedClient);

			seedClient.Config.SetConfig(ReplicationConstants.RavenReplicationTimeout, new NameValueCollection()
			                                                                          	{
			                                                                          		{"value", "\"00:00:00\""}
			                                                                          	}).Wait();
			Assert.DoesNotThrow(() => seedClient.Synchronization.StartSynchronizationAsync(sourceClient.ServerUrl, "test.bin").Wait());
		}

        private void UploadFilesSynchronously(out RavenFileSystemClient sourceClient, out RavenFileSystemClient seedClient, string fileName = "test.bin")
        {
            sourceClient = NewClient(1);
            seedClient = NewClient(0);

            var sourceContent = new RandomlyModifiedStream(new RandomStream(10, 1), 0.01);
            var seedContent = new RandomlyModifiedStream(new RandomStream(10, 1), 0.01);

            seedClient.UploadAsync(fileName, EmptyData, seedContent).Wait();
            sourceClient.UploadAsync(fileName, EmptyData, sourceContent).Wait();
        }

        private static Exception ExecuteAndGetInnerException(Action action)
        {
            Exception innerException = null;

            try
            {
                action();
            }
            catch (AggregateException exception)
            {
                innerException = exception.InnerException;
            }

            return innerException;
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
			                      		{"value", "{\"ReplicationSource\":\"http://localhost:19081\",\"FileLockedAt\":\"\\/Date(" + fileLockedDate.Ticks + ")\\/\"}"},
			                      	};
		}

		private static void ZeroTimeoutTest(RavenFileSystemClient seedClient, Action action)
		{
			seedClient.Config.SetConfig(SynchronizationHelper.SyncNameForFile("test.bin"), SynchronizationConfig(DateTime.MinValue)).Wait();

			seedClient.Config.SetConfig(ReplicationConstants.RavenReplicationTimeout, new NameValueCollection()
			                                                                          	{
			                                                                          		{"value", "\"00:00:00\""}
			                                                                          	}).Wait();

			Assert.DoesNotThrow(() => action());
		}
    }
}
