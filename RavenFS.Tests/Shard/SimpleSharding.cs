using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Client.Shard;
using Xunit;

namespace RavenFS.Tests.Shard
{
	public class SimpleSharding : MultiHostTestBase
	{
		ShardedRavenFileSystemClient shardedClient;

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
			var expected = new string('a', 5);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			await shardedClient.UploadAsync("abc.txt", ms);

			var result = shardedClient.DownloadAsync("abc.txt", ms);
			Assert.NotNull(ms);
		}
	}
}
