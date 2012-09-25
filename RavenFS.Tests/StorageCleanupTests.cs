namespace RavenFS.Tests
{
	using System.IO;
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
	}
}