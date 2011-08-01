using System.Net;
using Xunit;

namespace RavenFS.Tests
{
	public class FileHandling : ServerTest
	{
		[Fact]
		public void CanOverwriteFiles()
		{
			webClient.UploadString("/files/abc.txt", "PUT", "abcd");
			webClient.UploadString("/files/abc.txt", "PUT", "efcg");

			var str = webClient.DownloadString("/files/abc.txt");
			Assert.Equal("efcg", str);
		}

		[Fact]
		public void CanDeleteFiles()
		{
			webClient.UploadString("/files/abc.txt", "PUT", "abcd");
			webClient.UploadString("/files/abc.txt", "DELETE", "");

			var webException = Assert.Throws<WebException>(()=>webClient.DownloadString("/files/abc.txt"));
			Assert.Equal(HttpStatusCode.NotFound, ((HttpWebResponse)webException.Response).StatusCode);
		}
	}
}