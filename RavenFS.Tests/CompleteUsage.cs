using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests
{
	public class CompleteUsage : ServerTest
	{
		[Fact]
		public void HowToUseTheClient()
		{
			var client = new RavenFileSystemClient("http://localhost:9090");
			var uploadTask = client.Upload("dragon.design", new NameValueCollection
			{
				{"Customer", "Northwind"},
				{"Preferred", "True"}
			}, new MemoryStream(new byte[] {1, 2, 3}));

			uploadTask.Wait(); // or we can just let it run

			var searchTask = client.Search("Customer:Northwind AND Preferred:True");

			searchTask.Wait();

			Assert.Equal("dragon.design", searchTask.Result[0].Name);
		}
	}
}