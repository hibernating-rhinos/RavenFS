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

	    public int NumberOfShards
	    {
	        get { return ShardClients.Count; }
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

		public async Task<FileInfo[]> BrowseAsync(int pageSize = 25, PagingInfo pagingInfo = null)
		{
            if(pagingInfo == null)
                pagingInfo = new PagingInfo(ShardClients.Count);

            var indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            if (indexes == null)
            {
                var lastPage = pagingInfo.GetLastPageNumber();
                if(pagingInfo.CurrentPage - lastPage > 10)
                    throw new InvalidOperationException("Not Enough info in order to calculate requested page in a timely fation, last page info is for page #" + lastPage + ", please go to a closer page");

                var originalPage = pagingInfo.CurrentPage;
                pagingInfo.CurrentPage = lastPage;
                while (pagingInfo.CurrentPage < originalPage)
                {
                    await BrowseAsync(pageSize, pagingInfo);
                    pagingInfo.CurrentPage++;
                }

                indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            }

		    var results = new List<FileInfo>();

            var applyAsync =
               await
               ShardStrategy.ShardAccessStrategy.ApplyAsync(ShardClients.Values.ToList(), new ShardRequestData(),
                                                            (client, i) => client.BrowseAsync(indexes[i], pageSize));
		    var originalIndexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            while (results.Count < pageSize)
            {
                var item = GetSmallest(applyAsync, indexes, originalIndexes);
                if (item == null)
                    break;

                results.Add(item);
            }

		    pagingInfo.SetPagingInfo(indexes);

		    return results.ToArray();
		}

	    private FileInfo GetSmallest(FileInfo[][] applyAsync, int[] indexes, int[] originalIndexes)
	    {
	        FileInfo smallest = null;
	        var smallestIndex = -1;
	        for (var i = 0; i < applyAsync.Length; i++)
	        {

	            var pos = indexes[i] - originalIndexes[i];
	            if (pos >= applyAsync[i].Length)
	                continue;

	            var current = applyAsync[i][pos];
	            if (smallest == null ||
	                string.Compare(current.Name, smallest.Name, StringComparison.InvariantCultureIgnoreCase) < 0)
	            {
	                smallest = current;
	                smallestIndex = i;
	            }
	        }

	        if (smallestIndex != -1)
	            indexes[smallestIndex]++;

	        return smallest;
	    }

        public async Task<string[]> GetSearchFieldsAsync(int pageSize = 25, PagingInfo pagingInfo = null)
		{
            if (pagingInfo == null)
                pagingInfo = new PagingInfo(ShardClients.Count);

            var indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            if (indexes == null)
            {
                var lastPage = pagingInfo.GetLastPageNumber();
                if (pagingInfo.CurrentPage - lastPage > 10)
                    throw new InvalidOperationException("Not Enough info in order to calculate requested page in a timely fation, last page info is for page #" + lastPage + ", please go to a closer page");

                var originalPage = pagingInfo.CurrentPage;
                pagingInfo.CurrentPage = lastPage;
                while (pagingInfo.CurrentPage < originalPage)
                {
                    await GetSearchFieldsAsync(pageSize, pagingInfo);
                    pagingInfo.CurrentPage++;
                }

                indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            }

            var results = new List<string>();

            var applyAsync =
               await
               ShardStrategy.ShardAccessStrategy.ApplyAsync(ShardClients.Values.ToList(), new ShardRequestData(),
                                                            (client, i) => client.GetSearchFieldsAsync(indexes[i], pageSize));

            var originalIndexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            while (results.Count < pageSize)
            {
                var item = GetSmallest(applyAsync, indexes, originalIndexes);
                if (item == null)
                    break;

                results.Add(item);
            }

            pagingInfo.SetPagingInfo(indexes);

            return results.ToArray();
		}

		public Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
		{
            throw new NotImplementedException();
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

        public Task<string> UploadAsync(string filename, Stream source)
		{
            return UploadAsync(filename, new NameValueCollection(), source, null);
		}

        public Task<string> UploadAsync(string filename, NameValueCollection metadata, Stream source)
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

        public async Task<string[]> GetFoldersAsync(string @from = null, int pageSize = 25, PagingInfo pagingInfo = null)
		{
            if (pagingInfo == null)
                pagingInfo = new PagingInfo(ShardClients.Count);

            var indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            if (indexes == null)
            {
                var lastPage = pagingInfo.GetLastPageNumber();
                if (pagingInfo.CurrentPage - lastPage > 10)
                    throw new InvalidOperationException("Not Enough info in order to calculate requested page in a timely fation, last page info is for page #" + lastPage + ", please go to a closer page");

                var originalPage = pagingInfo.CurrentPage;
                pagingInfo.CurrentPage = lastPage;
                while (pagingInfo.CurrentPage < originalPage)
                {
                    await GetFoldersAsync(from, pageSize, pagingInfo);
                    pagingInfo.CurrentPage++;
                }

                indexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            }

            var results = new List<string>();

            var applyAsync =
               await
               ShardStrategy.ShardAccessStrategy.ApplyAsync(ShardClients.Values.ToList(), new ShardRequestData(),
                                                            (client, i) => client.GetFoldersAsync(from, indexes[i], pageSize));

            var originalIndexes = pagingInfo.GetPagingInfo(pagingInfo.CurrentPage);
            while (results.Count < pageSize)
            {
                var item = GetSmallest(applyAsync, indexes, originalIndexes);
                if (item == null)
                    break;

                results.Add(item);
            }

            pagingInfo.SetPagingInfo(indexes);

            return results.ToArray();
		}

        private string GetSmallest(string[][] applyAsync, int[] indexes, int[] originalIndexes)
	    {
            string smallest = null;
            var smallestIndex = -1;
            for (var i = 0; i < applyAsync.Length; i++)
            {

                var pos = indexes[i] - originalIndexes[i];
                if (pos >= applyAsync[i].Length)
                    continue;

                var current = applyAsync[i][pos];
                if (smallest != null && string.Compare(current, smallest, StringComparison.InvariantCultureIgnoreCase) >= 0) 
                    continue;

                smallest = current;
                smallestIndex = i;
            }

            if (smallestIndex != -1)
                indexes[smallestIndex]++;

            return smallest;
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
