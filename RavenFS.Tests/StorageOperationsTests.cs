namespace RavenFS.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Extensions;
	using Storage;
	using Synchronization;
	using Synchronization.IO;
	using Util;
	using Xunit;

	public class StorageOperationsTests : WebApiTest
	{
		[Fact]
		public void Can_force_storage_cleanup_from_client()
		{
			var client = NewClient();
			client.UploadAsync("toDelete.bin", new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })).Wait();

			client.DeleteAsync("toDelete.bin").Wait();

			client.Storage.CleanUp().Wait();

			var configNames = client.Config.GetConfigNames().Result;

			Assert.DoesNotContain(RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("toDelete.bin")), configNames);
		}

		[Fact]
		public void Should_create_apropriate_config_after_indicating_file_to_delete()
		{
			var client = NewClient();
			var rfs = GetRavenFileSystem();

			client.UploadAsync("toDelete.bin", new MemoryStream(new byte[] {1, 2, 3, 4, 5})).Wait();

			rfs.StorageOperationsTask.IndicateFileToDelete("toDelete.bin");

			DeleteFile deleteFile = null;
			rfs.Storage.Batch(
				accessor =>
				deleteFile = accessor.GetConfigurationValue<DeleteFile>(
					RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("toDelete.bin"))));

			Assert.Equal(RavenFileNameHelper.DeletingFileName("toDelete.bin"), deleteFile.CurrentFileName);
			Assert.Equal("toDelete.bin", deleteFile.OriginalFileName);
		}

		[Fact]
		public void Should_remove_file_deletion_config_after_storage_cleanup()
		{
			var client = NewClient();
			var rfs = GetRavenFileSystem();

			client.UploadAsync("toDelete.bin", new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })).Wait();

			rfs.StorageOperationsTask.IndicateFileToDelete("toDelete.bin");

			rfs.StorageOperationsTask.CleanupDeletedFilesAsync().Wait();

			IEnumerable<string> configNames = null;
			rfs.Storage.Batch(accessor => configNames = accessor.GetConfigNames(0, 10).ToArray());

			Assert.DoesNotContain(RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("toDelete.bin")), configNames);
		}

		[Fact]
		public void Should_remove_deleting_file_and_its_pages_after_storage_cleanup()
		{
			const int numberOfPages = 10;

			var client = NewClient();
			var rfs = GetRavenFileSystem();

			var bytes = new byte[numberOfPages * StorageConstants.MaxPageSize];
			new Random().NextBytes(bytes);

			client.UploadAsync("toDelete.bin", new MemoryStream(bytes)).Wait();

			rfs.StorageOperationsTask.IndicateFileToDelete("toDelete.bin");

			rfs.StorageOperationsTask.CleanupDeletedFilesAsync().Wait();

			Assert.Throws(typeof (FileNotFoundException),
			              () =>
						  rfs.Storage.Batch(accessor => accessor.GetFile(RavenFileNameHelper.DeletingFileName("toDelete.bin"), 0, 10)));

			for (int i = 1; i <= numberOfPages; i++)
			{
				int pageId = 0;
				rfs.Storage.Batch(accessor => pageId = accessor.ReadPage(i, null));
				Assert.Equal(-1, pageId); // if page does not exist we return -1
			}
		}

		[Fact]
		public void Should_not_perform_file_delete_if_it_is_being_synced()
		{
			var client = NewClient();
			var rfs = GetRavenFileSystem();

			client.UploadAsync("file.bin", new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })).Wait();

			rfs.StorageOperationsTask.IndicateFileToDelete("file.bin");

			rfs.Storage.Batch(accessor => accessor.SetConfig(RavenFileNameHelper.SyncLockNameForFile("file.bin"), LockFileTests.SynchronizationConfig(DateTime.UtcNow)));

			rfs.StorageOperationsTask.CleanupDeletedFilesAsync().Wait();

			DeleteFile deleteFile = null;

			rfs.Storage.Batch(accessor => deleteFile = accessor.GetConfigurationValue<DeleteFile>(
				RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("file.bin"))));

			Assert.Equal(RavenFileNameHelper.DeletingFileName("file.bin"), deleteFile.CurrentFileName);
			Assert.Equal("file.bin", deleteFile.OriginalFileName);
		}

		[Fact]
		public void Should_not_delete_downloading_file_if_synchronization_retry_is_being_performed()
		{
			var fileName = "file.bin";
			var downloadingFileName = RavenFileNameHelper.DownloadingFileName(fileName);

			var client = NewClient();
			var rfs = GetRavenFileSystem();

			client.UploadAsync(fileName, new RandomStream(1)).Wait();
			
			client.UploadAsync(downloadingFileName, new RandomStream(1)).Wait();

			rfs.StorageOperationsTask.IndicateFileToDelete(downloadingFileName);

			rfs.Storage.Batch(
				accessor =>
				accessor.SetConfig(RavenFileNameHelper.SyncLockNameForFile(fileName),
				                   LockFileTests.SynchronizationConfig(DateTime.UtcNow)));

			rfs.StorageOperationsTask.CleanupDeletedFilesAsync().Wait();

			DeleteFile deleteFile = null;
			rfs.Storage.Batch(accessor => deleteFile = accessor.GetConfigurationValue<DeleteFile>(
				RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName(downloadingFileName))));

			Assert.Equal(RavenFileNameHelper.DeletingFileName(downloadingFileName), deleteFile.CurrentFileName);
			Assert.Equal(downloadingFileName, deleteFile.OriginalFileName);
		}
	}
}