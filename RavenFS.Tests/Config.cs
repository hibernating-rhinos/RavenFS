using System.Collections.Specialized;
using Xunit;

namespace RavenFS.Tests
{
	public class Config : WebApiTest
	{
		[Fact]
		public void CanGetConfig_NotThere()
		{
			var client = NewClient();

			Assert.Null(client.Config.GetConfig("test").Result);
		}

		[Fact]
		public void CanSetConfig()
		{
			var client = NewClient();

			Assert.Null(client.Config.GetConfig("test").Result);

			client.Config.SetConfig("test", new NameValueCollection
			{
				{"test", "there"},
				{"hi", "you"}
			}).Wait();
			var nameValueCollection = client.Config.GetConfig("test").Result;
			Assert.NotNull(nameValueCollection);

			Assert.Equal("there", nameValueCollection["test"]);
			Assert.Equal("you", nameValueCollection["hi"]);

		}


		[Fact]
		public void CanGetConfigNames()
		{
			var client = NewClient();

			Assert.Null(client.Config.GetConfig("test").Result);

			client.Config.SetConfig("test", new NameValueCollection
			{
				{"test", "there"},
				{"hi", "you"}
			}).Wait();

			client.Config.SetConfig("test2", new NameValueCollection
			{
				{"test", "there"},
				{"hi", "you"}
			}).Wait();
			var names = client.Config.GetConfigNames().Result;
			Assert.Equal(new[]{"Raven/Sequences/Raven/Etag", "test", "test2"}, names);
		}

		[Fact]
		public void CanDelConfig()
		{
			var client = NewClient();

			Assert.Null(client.Config.GetConfig("test").Result);

			client.Config.SetConfig("test", new NameValueCollection
			{
				{"test", "there"},
				{"hi", "you"}
			}).Wait();
			Assert.NotNull(client.Config.GetConfig("test").Result);

			client.Config.DeleteConfig("test").Wait();

			Assert.Null(client.Config.GetConfig("test").Result);
		}

	    [Fact]
	    public void CanGetTotalConfigCount()
	    {
	        var client = NewClient();

	        client.Config.SetConfig("TestConfigA", new NameValueCollection()).Wait();
	        client.Config.SetConfig("TestConfigB", new NameValueCollection()).Wait();

	        Assert.Equal(2, client.Config.SearchAsync(prefix: "Test").Result.TotalCount);
	    }

        [Fact]
        public void SearchResultsOnlyIncludeConfigsWithPrefix()
        {
            var client = NewClient();

            client.Config.SetConfig("TestConfigA", new NameValueCollection()).Wait();
            client.Config.SetConfig("TestConfigB", new NameValueCollection()).Wait();
            client.Config.SetConfig("AnotherB", new NameValueCollection()).Wait();

            Assert.Equal(2, client.Config.SearchAsync(prefix: "Test").Result.TotalCount);
        }
	}
}