namespace RavenFS.Tests.Bugs
{
	using System.IO;
	using System.Linq;
	using Client;
	using Extensions;
	using Synchronization;
	using Xunit;

	public class SynchronizationAfterSetUpDestinations : MultiHostTestBase
	{
		[Fact]
		public void Should_transfer_entire_file_even_if_rename_operation_was_performed()
		{
			var source = NewClient(0);
			var destination = NewClient(1);

			var fileContent = new MemoryStream(new byte[] {1, 2, 3});
			source.UploadAsync("test.bin", fileContent).Wait();
			source.RenameAsync("test.bin", "renamed.bin").Wait();

			SyncTestUtils.TurnOnSynchronization(source, destination);

			var destinationSyncResults = source.Synchronization.SynchronizeDestinationsAsync().Result;
			Assert.Equal(1, destinationSyncResults.Length);

			var reports = destinationSyncResults[0].Reports.ToArray();
			Assert.Null(reports[0].Exception);
			Assert.Equal(SynchronizationType.ContentUpdate, reports[0].Type);
			Assert.Equal("renamed.bin", reports[0].FileName);

			fileContent.Position = 0;
			Assert.Equal(fileContent.GetMD5Hash(), destination.GetMetadataForAsync("renamed.bin").Result["Content-MD5"]);
		}
	}
}