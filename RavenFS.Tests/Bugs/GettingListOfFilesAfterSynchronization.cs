using System.IO;
using Xunit;

namespace RavenFS.Tests.Bugs
{
	public class GettingListOfFilesAfterSynchronization : MultiHostTestBase
	{
		[Fact]
		public void Should_work()
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 100);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			var sourceClient = NewClient(0);
			var destinationClient = NewClient(1);

			string fileName = "abc.txt";
			sourceClient.UploadAsync(fileName, ms).Wait();
			sourceClient.Synchronization.StartAsync(fileName, destinationClient.ServerUrl).Wait();

			var destinationFiles = destinationClient.GetFilesAsync("/").Result;
			Assert.True(destinationFiles.FileCount == 1, "count not one");
			Assert.True(destinationFiles.Files.Length == 1, "not one file");
			Assert.True(destinationFiles.Files[0].Name == fileName, "name doesnt match");
		}
	}
}