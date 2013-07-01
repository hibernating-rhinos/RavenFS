using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Client.Shard;
using Xunit;

namespace RavenFS.Tests.Shard
{
	public class SimpleSharding : MultiHostTestBase
	{
	    readonly ShardedRavenFileSystemClient shardedClient;

		public SimpleSharding()
		{
			var client1 = NewClient(0);
			var client2 = NewClient(1);
			shardedClient = new ShardedRavenFileSystemClient(new ShardStrategy(new Dictionary<string, RavenFileSystemClient>
				{
					{"1", client1},
					{"2", client2},
				}));
		}

		[Fact]
		public void CanGetSharding()
		{
			var shards = shardedClient.GetShardsToOperateOn(new ShardRequestData{Keys = new List<string>{"test.bin"}});
			Assert.Equal(shards.Count, 2);
		}

		[Fact]
		public async Task CanGetFileFromSharding()
		{       
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;
            var newFileName = await shardedClient.UploadAsync("abc.txt", ms);

            var ms2 = new MemoryStream();
            await shardedClient.DownloadAsync(newFileName, ms2);

            ms2.Position = 0;

            var actual = new StreamReader(ms2).ReadToEnd();
            Assert.Equal(expected, actual);
		}

	    [Fact]
	    public async Task CanBrowseWithSharding()
	    {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;

            await shardedClient.UploadAsync("a.txt", ms);
            await shardedClient.UploadAsync("b.txt", ms);
            await shardedClient.UploadAsync("c.txt", ms);
            await shardedClient.UploadAsync("d.txt", ms);
            await shardedClient.UploadAsync("e.txt", ms);

	        var pagingInfo = new PagingInfo(shardedClient.NumberOfShards);
	        var result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(2, result.Length);

	        pagingInfo.CurrentPage++;
            result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(2, result.Length);

            pagingInfo.CurrentPage++;
            result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(1, result.Length);

            pagingInfo.CurrentPage++;
            result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(0, result.Length);
	    }

        [Fact]
        public async Task CanBrowseToAdvancedPageWithSharding()
        {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;

            await shardedClient.UploadAsync("a.txt", ms);
            await shardedClient.UploadAsync("b.txt", ms);
            await shardedClient.UploadAsync("c.txt", ms);
            await shardedClient.UploadAsync("d.txt", ms);
            await shardedClient.UploadAsync("e.txt", ms);

            var pagingInfo = new PagingInfo(shardedClient.NumberOfShards){CurrentPage = 2};
            var result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(1, result.Length);

            pagingInfo.CurrentPage++;
            result = await shardedClient.BrowseAsync(2, pagingInfo);
            Assert.Equal(0, result.Length);

        }

        [Fact]
        public async Task CanNotBrowseToPageFarAway()
        {
            var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var expected = new string('a', 1024);
            streamWriter.Write(expected);
            streamWriter.Flush();
            ms.Position = 0;

            await shardedClient.UploadAsync("a.txt", ms);
            await shardedClient.UploadAsync("b.txt", ms);
            await shardedClient.UploadAsync("c.txt", ms);
            await shardedClient.UploadAsync("d.txt", ms);
            await shardedClient.UploadAsync("e.txt", ms);

            var pagingInfo = new PagingInfo(shardedClient.NumberOfShards) { CurrentPage = 20 };
            try
            {
                await shardedClient.BrowseAsync(2, pagingInfo);
                Assert.Equal(true, false);//Should not get here
            }
            catch (Exception exception)
            {
                Assert.IsType<InvalidOperationException>(exception);
            }
        }
	}
}
