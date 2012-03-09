using System.IO;
using Xunit;
using System.Linq;

namespace RavenFS.Tests
{
	public class Folders : WebApiTest
	{
		[Fact]
		public void CanGetListOfFolders()
		{
			var client = NewClient();
			var ms = new MemoryStream();
			client.UploadAsync("test/abc.txt", ms).Wait();
			client.UploadAsync("test/ced.txt", ms).Wait();
			client.UploadAsync("why/abc.txt", ms).Wait();

			var strings = client.GetFoldersAsync().Result;
			Assert.Equal(new[]{"test", "why"}, strings);
		}

		[Fact]
		public void CanGetListOfFilesInFolder()
		{
			var client = NewClient();
			var ms = new MemoryStream();
			client.UploadAsync("test/abc.txt", ms).Wait();
			client.UploadAsync("test/ced.txt", ms).Wait();
			client.UploadAsync("why/abc.txt", ms).Wait();

			var strings = client.GetFilesAsync("test").Result.Select(x=>x.Name).ToArray();
			Assert.Equal(new[] { "test/abc.txt", "test/ced.txt" }, strings);
		}

		[Fact]
		public void CanPage()
		{
			var client = NewClient();
			var ms = new MemoryStream();
			client.UploadAsync("test/abc.txt", ms).Wait();
			client.UploadAsync("test/ced.txt", ms).Wait();
			client.UploadAsync("why/abc.txt", ms).Wait();
			client.UploadAsync("why1/abc.txt", ms).Wait();

			var strings = client.GetFoldersAsync("test").Result;
			Assert.Equal(new[] { "why", "why1" }, strings);
		}

		[Fact]
		public void CanDetectRemoval()
		{
			var client = NewClient();
			var ms = new MemoryStream();
			client.UploadAsync("test/abc.txt", ms).Wait();
			client.UploadAsync("test/ced.txt", ms).Wait();
			client.UploadAsync("why/abc.txt", ms).Wait();
			client.UploadAsync("why1/abc.txt", ms).Wait();

			client.DeleteAsync("why1/abc.txt").Wait();

			var strings = client.GetFoldersAsync().Result;
			Assert.Equal(new[] { "test", "why" }, strings);
		}
	}
}