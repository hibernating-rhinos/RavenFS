using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenFS.Client.Shard
{
	public class ShardedRavenFileSystemClient : IRavenFileSystemClient
	{
		protected readonly ShardStrategy shardStrategy;
		protected readonly IDictionary<string, RavenFileSystemClient> shardClients;

		private string lastUsedClient = "";

		public ShardedRavenFileSystemClient(ShardStrategy strategy)
		{
			shardStrategy = strategy;
			shardClients = strategy.Shards;
		}

		#region Sharding support methods

		private RavenFileSystemClient GetClientToWorkOn()
		{
			if (string.IsNullOrWhiteSpace(lastUsedClient))
			{
				var item = shardClients.OrderBy(pair => pair.Key).First();
				lastUsedClient = item.Key;
				return item.Value;
			}
			var takeNext = false;
			foreach (var pair in shardClients.OrderBy(pair => pair.Key))
			{
				if (takeNext)
				{
					lastUsedClient = pair.Key;
					return pair.Value;
				}

				if (pair.Key == lastUsedClient)
					takeNext = true;
			}

			var result = shardClients.OrderBy(pair => pair.Key).First();
			lastUsedClient = result.Key;
			return result.Value;
		}

		private RavenFileSystemClient GetSingleClient(string filename)
		{
			var shards = GetShardsToOperateOn(new ShardRequestData { Keys = new List<string> { filename } });
			var client = shards.Count != 1 ? GetClientToWorkOn() : shards.First().Item2;
			return client;
		}

		public IList<Tuple<string, RavenFileSystemClient>> GetShardsToOperateOn(ShardRequestData resultionData)
		{
			var shardIds = shardStrategy.ShardResolutionStrategy.PotentialShardsFor(resultionData);

			IEnumerable<KeyValuePair<string, RavenFileSystemClient>> cmds = shardClients;

			if (shardIds == null)
			{
				return cmds.Select(x => Tuple.Create(x.Key, x.Value)).ToList();
			}

			var list = new List<Tuple<string, RavenFileSystemClient>>();
			foreach (var shardId in shardIds)
			{
				RavenFileSystemClient value;
				if (shardClients.TryGetValue(shardId, out value) == false)
					throw new InvalidOperationException("Could not find shard id: " + shardId);

				list.Add(Tuple.Create(shardId, value));

			}
			return list;
		}

		protected IList<RavenFileSystemClient> GetCommandsToOperateOn(ShardRequestData resultionData)
		{
			return GetShardsToOperateOn(resultionData).Select(x => x.Item2).ToList();
		}

		//protected Dictionary<string, SaveChangesData> GetChangesToSavePerShard(SaveChangesData data)
		//{
		//	var saveChangesPerShard = new Dictionary<string, SaveChangesData>();

		//	foreach (var deferredCommands in deferredCommandsByShard)
		//	{
		//		var saveChangesData = saveChangesPerShard.GetOrAdd(deferredCommands.Key);
		//		saveChangesData.DeferredCommandsCount += deferredCommands.Value.Count;
		//		saveChangesData.Commands.AddRange(deferredCommands.Value);
		//	}
		//	deferredCommandsByShard.Clear();

		//	for (int index = 0; index < data.Entities.Count; index++)
		//	{
		//		var entity = data.Entities[index];
		//		var metadata = GetMetadataFor(entity);
		//		var shardId = metadata.Value<string>(Constants.RavenShardId);

		//		var shardSaveChangesData = saveChangesPerShard.GetOrAdd(shardId);
		//		shardSaveChangesData.Entities.Add(entity);
		//		shardSaveChangesData.Commands.Add(data.Commands[index]);
		//	}
		//	return saveChangesPerShard;
		//}

		protected struct IdToLoad<T>
		{
			public IdToLoad(string id, IList<RavenFileSystemClient> shards)
			{
				this.Id = id;
				this.Shards = shards;
			}

			public readonly string Id;
			public readonly IList<RavenFileSystemClient> Shards;
		}

		#endregion

		public Task<ServerStats> StatsAsync()
		{
			throw new NotImplementedException();
		}

		public async Task DeleteAsync(string filename)
		{
			var shards = GetShardsToOperateOn(new ShardRequestData {Keys = new List<string> {filename}});

			foreach (var shard in shards)
			{
				await shard.Item2.DeleteAsync(filename);
			}
		}

		public Task RenameAsync(string filename, string rename)
		{
			throw new NotImplementedException();
		}

		public Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25)
		{
			throw new NotImplementedException();
		}

		public Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25)
		{
			throw new NotImplementedException();
		}

		public Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
		{
			throw new NotImplementedException();
		}

		public async Task<NameValueCollection> GetMetadataForAsync(string filename)
		{
			var client = GetSingleClient(filename);
			return await client.GetMetadataForAsync(filename);
		}

		public async Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long? @from = null, long? to = null)
		{
			var client = GetSingleClient(filename);
			return await client.DownloadAsync(filename, destination, from, to);
		}

		public async Task UpdateMetadataAsync(string filename, NameValueCollection metadata)
		{
			var client = GetSingleClient(filename);

			await client.UpdateMetadataAsync(filename, metadata);
		}

		public async Task UploadAsync(string filename, Stream source)
		{
			var client = GetSingleClient(filename);

			await client.UploadAsync(filename, source);
		}

		public async Task UploadAsync(string filename, NameValueCollection metadata, Stream source)
		{
			var client = GetSingleClient(filename);

			await client.UploadAsync(filename, metadata, source);
		}

		public async Task UploadAsync(string filename, NameValueCollection metadata, Stream source, Action<string, long> progress)
		{
			var client = GetSingleClient(filename);

			await client.UploadAsync(filename, metadata, source, progress);
		}

		public Task<string[]> GetFoldersAsync(string @from = null, int start = 0, int pageSize = 25)
		{
			throw new NotImplementedException();
		}

		public Task<SearchResults> GetFilesAsync(string folder, FilesSortOptions options = FilesSortOptions.Default, string fileNameSearchPattern = "", int start = 0,
		                          int pageSize = 25)
		{
			throw new NotImplementedException();
		}

		public Task<Guid> GetServerId()
		{
			throw new NotImplementedException();
		}
	}
}
