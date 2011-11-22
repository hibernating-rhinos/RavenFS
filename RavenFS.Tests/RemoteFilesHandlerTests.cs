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

		[Fact]
		public void CanGetFilesList()
		{
			webClient.UploadString("/files/abc.txt", "PUT", "abc");
			var str = webClient.DownloadString("/files");
			Assert.Equal("[{\"Name\":\"abc.txt\",\"TotalSize\":3,\"UploadedSize\":3,\"HumaneTotalSize\":\"3 Bytes\",\"HumaneUploadedSize\":\"3 Bytes\",\"Metadata\":{}}]", str);
		}

		[Fact]
		public void CanSetFileMetadata_Then_GetItFromFilesList()
		{
			webClient.Headers["Test"] = "Value";
			webClient.UploadString("/files/abc.txt", "PUT", "abc");
			var str = webClient.DownloadString("/files");
			Assert.Equal("[{\"Name\":\"abc.txt\",\"TotalSize\":3,\"UploadedSize\":3,\"HumaneTotalSize\":\"3 Bytes\",\"HumaneUploadedSize\":\"3 Bytes\",\"Metadata\":{\"Test\":\"Value\"}}]", str);
		}

		[Fact]
		public void CanSetFileMetadata_Then_GetItFromFile()
		{
			webClient.Headers["Test"] = "Value";
			webClient.UploadString("/files/abc.txt", "PUT", "abc");
			var str = webClient.DownloadString("/files/abc.txt");
			Assert.Equal("abc", str);
			Assert.Equal("Value", webClient.ResponseHeaders["Test"]);
		}
	}
}