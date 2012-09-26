namespace RavenFS.Tests
{
	using System;
	using System.IO;
	using Storage;
	using Util;
	using Xunit;

	public class StorageCleanupTests : WebApiTest
	{
		[Fact]
		public void Should_create_apropriate_config_after_file_delete()
		{
			var client = NewClient();
			client.UploadAsync("toDelete.bin", new MemoryStream(new byte[] {1, 2, 3, 4, 5})).Wait();

			client.DeleteAsync("toDelete.bin").Wait();

			var config =
				client.Config.GetConfig(
					RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("toDelete.bin")))
					.Result;

			Assert.NotNull(config);
			Assert.Equal(RavenFileNameHelper.DeletingFileName("toDelete.bin"), config["value"].Trim('"'));
		}

		[Fact]
		public void Should_remove_file_deletion_config_after_storage_cleanup()
		{
			var client = NewClient();
			client.UploadAsync("toDelete.bin", new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })).Wait();

			client.DeleteAsync("toDelete.bin").Wait();

			client.Storage.CleanUp().Wait();

			var configNames = client.Config.GetConfigNames().Result;

			Assert.DoesNotContain(RavenFileNameHelper.DeletingFileConfigNameForFile(RavenFileNameHelper.DeletingFileName("toDelete.bin")), configNames);
		}

		[Fact]
		public void Should_remove_deleting_file_and_its_pages_after_storage_cleanup()
		{
			const int numberOfPages = 10;

			var client = NewClient();

			var bytes = new byte[numberOfPages * StorageConstants.MaxPageSize];
			new Random().NextBytes(bytes);

			client.UploadAsync("toDelete.bin", new MemoryStream(bytes)).Wait();

			client.DeleteAsync("toDelete.bin").Wait();

			client.Storage.CleanUp().Wait();

			var storage = GetRavenFileSystem().Storage;

			Assert.Throws(typeof (FileNotFoundException),
			              () =>
			              storage.Batch(accessor => accessor.GetFile(RavenFileNameHelper.DeletingFileName("toDelete.bin"), 0, 10)));

			for (int i = 1; i <= numberOfPages; i++)
			{
				int pageId = 0;
				storage.Batch(accessor => pageId = accessor.ReadPage(i, null));
				Assert.Equal(-1, pageId); // if page does not exist we return -1
			}
		}
	}
}