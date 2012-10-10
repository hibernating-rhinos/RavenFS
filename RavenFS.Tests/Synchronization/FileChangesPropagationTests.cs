namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using Client;
	using Extensions;
	using IO;
	using Xunit;
	using Xunit.Extensions;

	public class FileChangesPropagationTests : MultiHostTestBase
	{
		private const int AddtitionalServerInstancePortNumber = 19083;

		[Fact]
		public void File_rename_should_be_propagated()
		{
			StartServerInstance(AddtitionalServerInstancePortNumber);

			var content = new MemoryStream(new byte[] { 1, 2, 3 });

			var server1 = NewClient(0);
			var server2 = NewClient(1);
			var server3 = new RavenFileSystemClient(ServerAddress(AddtitionalServerInstancePortNumber));

			content.Position = 0;
			server1.UploadAsync("test.bin", new NameValueCollection() { { "test", "value" } }, content).Wait();

			SyncTestUtils.TurnOnSynchronization(server1, server2);

			Assert.Null(server1.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			SyncTestUtils.TurnOnSynchronization(server2, server3);

			Assert.Null(server2.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			SyncTestUtils.TurnOffSynchronization(server1);

			server1.RenameAsync("test.bin", "rename.bin").Wait();

			SyncTestUtils.TurnOnSynchronization(server1, server2);

			Assert.Null(server1.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			Assert.Null(server2.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			// On all servers should be file named "rename.bin"
			Assert.Equal(1, server1.BrowseAsync().Result.Count());
			Assert.Equal("rename.bin", server1.BrowseAsync().Result[0].Name);

			Assert.Equal(1, server2.BrowseAsync().Result.Count());
			Assert.Equal("rename.bin", server2.BrowseAsync().Result[0].Name);

			Assert.Equal(1, server3.BrowseAsync().Result.Count());
			Assert.Equal("rename.bin", server3.BrowseAsync().Result[0].Name);
		}

		[Theory]
		[InlineData(1024)]
		[InlineData(1024 * 1024 * 2)]
		public void File_content_change_should_be_propagated(int size)
		{
			StartServerInstance(AddtitionalServerInstancePortNumber);

			var buffer = new byte[size];
			new Random().NextBytes(buffer);

			var content = new MemoryStream(buffer);
			var changedContent = new RandomlyModifiedStream(content, 0.01);

			var server1 = NewClient(0);
			var server2 = NewClient(1);
			var server3 = new RavenFileSystemClient(ServerAddress(AddtitionalServerInstancePortNumber));

			content.Position = 0;
			server1.UploadAsync("test.bin", new NameValueCollection() { { "test", "value" } }, content).Wait();

			SyncTestUtils.TurnOnSynchronization(server1, server2);

			Assert.Null(server1.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			SyncTestUtils.TurnOnSynchronization(server2, server3);

			Assert.Null(server2.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			SyncTestUtils.TurnOffSynchronization(server1);

			content.Position = 0;
			server1.UploadAsync("test.bin", changedContent).Wait();

			SyncTestUtils.TurnOnSynchronization(server1, server2);

			Assert.Null(server1.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			Assert.Null(server2.Synchronization.SynchronizeDestinationsAsync().Result.ToArray()[0].Exception);

			// On all servers should have the same content of the file
			string server1Md5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				server1.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				server1Md5 = resultFileContent.GetMD5Hash();
			}

			string server2Md5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				server2.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				server2Md5 = resultFileContent.GetMD5Hash();
			}

			string server3Md5 = null;
			using (var resultFileContent = new MemoryStream())
			{
				server3.DownloadAsync("test.bin", resultFileContent).Wait();
				resultFileContent.Position = 0;
				server3Md5 = resultFileContent.GetMD5Hash();
			}

			Assert.Equal(server1Md5, server2Md5);
			Assert.Equal(server2Md5, server3Md5);
		}
	}
}