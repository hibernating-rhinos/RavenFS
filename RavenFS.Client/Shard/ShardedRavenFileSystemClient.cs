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

		public async Task<ServerStats> StatsAsync()
		{
			var shards = shardClients;
			var result = new ServerStats();
			foreach (var ravenFileSystemClient in shards.Values)
			{
				var stat = await ravenFileSystemClient.StatsAsync();

				result.FileCount += stat.FileCount;
			}

			return result;
		}

		public async Task DeleteAsync(string filename)
		{
			var client = GetSingleClient(filename);
			await client.DeleteAsync(filename);
		}

		public async Task RenameAsync(string filename, string rename)
		{
			var client = GetSingleClient(filename);
			await client.RenameAsync(filename, rename);
		}

		public async Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25)
		{
			var clients = shardClients.Values;
			var found = 0;
			var skip = start;
			var results = new List<FileInfo>();
			foreach (var ravenFileSystemClient in clients)
			{
				if (found >= pageSize)
					return results.ToArray();
				var local = await ravenFileSystemClient.BrowseAsync(skip, pageSize - found);
				found = local.Length;
				results.AddRange(local);
				if (local.Length == 0)
				{
					var fileCount = await ravenFileSystemClient.StatsAsync();
					skip -= (int)fileCount.FileCount;
					if (skip < 0)
						skip = 0;
				}
				else
				{
					skip = 0;
				}
			}

			return results.ToArray();
		}

		public async Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25)
		{
			var clients = shardClients.Values;
			var found = 0;
			var skip = start;
			var results = new List<string>();
			foreach (var ravenFileSystemClient in clients)
			{
				if (found >= pageSize)
					return results.ToArray();
				var local = await ravenFileSystemClient.GetSearchFieldsAsync(skip, pageSize - found);
				found = local.Length;
				results.AddRange(local);
				if (local.Length == 0)
				{
					var fileCount = await ravenFileSystemClient.StatsAsync();
					skip -= (int)fileCount.FileCount;
					if (skip < 0)
						skip = 0;
				}
				else
				{
					skip = 0;
				}
			}

			return results.ToArray();
		}

		public async Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
		{
			var clients = shardClients.Values;
			var found = 0;
			var skip = start;
			var result = new SearchResults();
			foreach (var ravenFileSystemClient in clients)
			{
				if (found >= pageSize)
					return result;
				var local = await ravenFileSystemClient.SearchAsync(query, sortFields, skip, pageSize - found);
				found = local.FileCount;

				if (local.FileCount == 0)
				{
					var fileCount = await ravenFileSystemClient.StatsAsync();
					skip -= (int)fileCount.FileCount;
					if (skip < 0)
						skip = 0;
				}
				else
				{
					var files = new List<FileInfo>();
					files.AddRange(result.Files);
					files.AddRange(local.Files);

					result.FileCount += local.FileCount;
					result.Files = files.ToArray();
					result.PageSize = pageSize;
					result.Start = start;
					skip = 0;
				}
			}

			return result;
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

		public async Task<string[]> GetFoldersAsync(string @from = null, int start = 0, int pageSize = 25)
		{
			var clients = shardClients.Values;
			var found = 0;
			var skip = start;
			var results = new List<string>();
			foreach (var ravenFileSystemClient in clients)
			{
				if (found >= pageSize)
					return results.ToArray();
				var local = await ravenFileSystemClient.GetFoldersAsync(from, skip, pageSize - found);
				found = local.Length;
				results.AddRange(local);
				if (local.Length == 0)
				{
					//TODO: update to Folders Count
					//var fileCount = await ravenFileSystemClient.StatsAsync();
					//skip -= (int)fileCount.FileCount;
					//if (skip < 0)
						skip = 0;
				}
				else
				{
					skip = 0;
				}
			}

			return results.ToArray();
		}

		public async Task<SearchResults> GetFilesAsync(string folder, FilesSortOptions options = FilesSortOptions.Default, string fileNameSearchPattern = "", int start = 0,
		                          int pageSize = 25)
		{
			var clients = shardClients.Values;
			var found = 0;
			var skip = start;
			var result = new SearchResults();
			foreach (var ravenFileSystemClient in clients)
			{
				if (found >= pageSize)
					return result;
				var local = await ravenFileSystemClient.GetFilesAsync(folder, options,fileNameSearchPattern, skip, pageSize - found);
				found = local.FileCount;

				if (local.FileCount == 0)
				{
					var fileCount = await ravenFileSystemClient.StatsAsync();
					skip -= (int)fileCount.FileCount;
					if (skip < 0)
						skip = 0;
				}
				else
				{
					var files = new List<FileInfo>();
					files.AddRange(result.Files);
					files.AddRange(local.Files);

					result.FileCount += local.FileCount;
					result.Files = files.ToArray();
					result.PageSize = pageSize;
					result.Start = start;
					skip = 0;
				}
			}

			return result;
		}

		public Task<Guid> GetServerId()
		{
			throw new NotImplementedException();
		}
	}
}
