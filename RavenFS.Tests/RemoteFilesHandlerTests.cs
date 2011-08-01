using Xunit;

namespace RavenFS.Tests
{
	public class RemoteFilesHandlerTests : ServerTest
	{
		[Fact]
		public void CanGetFilesList_Empty()
		{
			var str = webClient.DownloadString("/files");
			Assert.Equal("[]", str);
		}


		[Fact]
		public void CanPutFile()
		{
			var data = new string('a', 1024*128);
			webClient.UploadString("/files/abc.txt", "PUT", data);
			var downloadString = webClient.DownloadString("/files/abc.txt");
			Assert.Equal(data, downloadString);
		}
	}
}