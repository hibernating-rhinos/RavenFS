using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenFS.Client.Shard
{
	public class ShardedRavenFileSystemClient
	{
		protected readonly ShardStrategy ShardStrategy;
		protected readonly IDictionary<string, RavenFileSystemClient> ShardClients;

		public ShardedRavenFileSystemClient(ShardStrategy strategy)
		{
			ShardStrategy = strategy;
			ShardClients = strategy.Shards;
		}

		#region Sharding support methods

		public IList<Tuple<string, RavenFileSystemClient>> GetShardsToOperateOn(ShardRequestData resultionData)
		{
			var shardIds = ShardStrategy.ShardResolutionStrategy.PotentialShardsFor(resultionData);

			IEnumerable<KeyValuePair<string, RavenFileSystemClient>> cmds = ShardClients;

			if (shardIds == null)
			{
				return cmds.Select(x => Tuple.Create(x.Key, x.Value)).ToList();
			}

			var list = new List<Tuple<string, RavenFileSystemClient>>();
			foreach (var shardId in shardIds)
			{
				RavenFileSystemClient value;
				if (ShardClients.TryGetValue(shardId, out value) == false)
					throw new InvalidOperationException("Could not find shard id: " + shardId);

				list.Add(Tuple.Create(shardId, value));

			}
			return list;
		}

		protected IList<RavenFileSystemClient> GetCommandsToOperateOn(ShardRequestData resultionData)
		{
			return GetShardsToOperateOn(resultionData).Select(x => x.Item2).ToList();
		}

		#endregion

		public async Task<ServerStats> StatsAsync()
		{
		    var applyAsync =
		        await
		        ShardStrategy.ShardAccessStrategy.ApplyAsync(ShardClients.Values.ToList(), new ShardRequestData(),
		                                                     (client, i) => client.StatsAsync());

		    return new ServerStats
		        {
		            FileCount = applyAsync.Sum(x => x.FileCount)
		        };
		}

		public Task DeleteAsync(string filename)
		{
			var client = TryGetClintFromFileName(filename);
			return client.DeleteAsync(filename);
		}

		public Task RenameAsync(string filename, string rename)
		{
			var client = TryGetClintFromFileName(filename);
		    return client.RenameAsync(filename, rename);
		}

		public async Task<FileInfo[]> BrowseAsync(int pageSize = 25, PagingInfo pagingInfo = null, bool getNext = true)
		{
		    var shardIds = ShardClients.Keys.ToList();
            if(pagingInfo == null)
                pagingInfo = new PagingInfo(shardIds);

            var applyAsync =
               await
               ShardStrategy.ShardAccessStrategy.ApplyAsync(ShardClients.Values.ToList(), new ShardRequestData(),
                                                            (client, i) => client.BrowseAsync(pagingInfo.GetLastInfo(shardIds[i], getNext), pageSize));
		    var indexes = new int[shardIds.Count];
		    var results = new List<FileInfo>();

		    InitIndexes(pagingInfo, indexes,shardIds, getNext);

            while (results.Count < pageSize)
            {
                var item = GetSmallest(applyAsync, indexes);
                if (item == null)
                    break;

                results.Add(item);
            }

		    for (var i = 0; i < pagingInfo.ShardPagingInfos.Count; i++)
		    {
		        var shardId = shardIds[i];
		        pagingInfo.ShardPagingInfos[shardId].PageLocations.Add(indexes[i]);
		    }

		    return results.ToArray();
		}

	    private void InitIndexes(PagingInfo pagingInfo, int[] indexes,List<string> shardIds, bool getNext)
	    {
	        var delta = getNext ? 1 : 2;

	        for (var i = 0; i < pagingInfo.ShardPagingInfos.Count; i++)
	        {
		        var shardId = shardIds[i];
	            var index = pagingInfo.ShardPagingInfos[shardId].PageLocations.Count - delta;
	            indexes[i] = pagingInfo.ShardPagingInfos[shardId].PageLocations[index];
	        }   
        }

	    private FileInfo GetSmallest(FileInfo[][] applyAsync, int[] indexes)
	    {
	        FileInfo smallest = null;
	        var smallestIndex = -1;
	        var smallestPos = -1;
	        for (var i = 0; i < applyAsync.Length; i++)
	        {

	            var pos = indexes[i];
	            if (pos >= applyAsync[i].Length)
	                continue;

	            var current = applyAsync[i][pos];
	            if (smallest == null ||
	                string.Compare(current.Name, smallest.Name, StringComparison.InvariantCultureIgnoreCase) < 0)
	            {
	                smallest = current;
	                smallestIndex = i;
	                smallestPos = pos;
	            }
	        }

	        if (smallestIndex != -1)
	            indexes[smallestIndex] = smallestPos + 1;

	        return smallest;
	    }

	    public Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25)
		{
            throw new NotImplementedException();
		}

		public Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
		{
            throw new NotImplementedException();
            //var shardRequestData = new ShardRequestData
            //    {
            //        Query = query
            //    };
            //var potentialShards = ShardStrategy.ShardResolutionStrategy.PotentialShardsFor(shardRequestData);

            //var results = await ShardStrategy.ShardAccessStrategy.ApplyAsync(potentialShards.Select(s => ShardClients[s]).ToList(),
            //                                             shardRequestData,
            //                                             async (client, i) => await client.SearchAsync(query, sortFields, start, pageSize));
            
            //// merge & re-sort results
		}

	    public Task<NameValueCollection> GetMetadataForAsync(string filename)
		{
			var client = TryGetClintFromFileName(filename);
			return client.GetMetadataForAsync(filename);
		}

		public Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long? @from = null, long? to = null)
		{
			var client = TryGetClintFromFileName(filename);
			return client.DownloadAsync(filename, destination, from, to);
		}

		public Task UploadAsync(string filename, Stream source)
		{
            return UploadAsync(filename, new NameValueCollection(), source, null);
		}

		public  Task UploadAsync(string filename, NameValueCollection metadata, Stream source)
		{
		    return UploadAsync(filename, metadata, source, null);
		}

        public Task UpdateMetadataAsync(string filename, NameValueCollection metadata)
        {
            var client = TryGetClintFromFileName(filename);

            return client.UpdateMetadataAsync(filename, metadata);
        }

	    private RavenFileSystemClient TryGetClintFromFileName(string filename)
	    {
	        var clientId = ShardStrategy.ShardResolutionStrategy.GetShardIdFromFileName(filename);
	        var client = TryGetClient(clientId);
	        return client;
	    }

	    public async Task<string> UploadAsync(string filename, NameValueCollection metadata, Stream source, Action<string, long> progress)
		{
		    var resolutionResult = ShardStrategy.ShardResolutionStrategy.GetShardIdForUpload(filename, metadata);

		    var client = TryGetClient(resolutionResult.ShardId);

		    await client.UploadAsync(resolutionResult.NewFileName, metadata, source, progress);

            return resolutionResult.NewFileName;
		}

        private RavenFileSystemClient TryGetClient(string clientId)
        {
            try
            {
                return ShardClients[clientId];
            }
            catch (Exception)
            {
                throw new FileNotFoundException("Count not find shard client with the id:" + clientId);
            }
        }

	    public Task<string[]> GetFoldersAsync(string @from = null, int start = 0, int pageSize = 25)
		{
            throw new NotImplementedException();
		}

		public async Task<SearchResults> GetFilesAsync(string folder, FilesSortOptions options = FilesSortOptions.Default, string fileNameSearchPattern = "", int start = 0,
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
