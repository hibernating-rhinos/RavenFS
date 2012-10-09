namespace RavenFS.Tests.Synchronization
{
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using Client;
	using Xunit;

	public class UpdatesPropagationTests : MultiHostTestBase
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
	}
}