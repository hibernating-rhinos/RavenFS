using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenFS.Client.Shard
{
    public class ShardPagingInfo
    {
        public List<int> PageLocations { get; set; }

        public ShardPagingInfo()
        {
            PageLocations = new List<int> {0};
        }
    }

    public class PagingInfo
    {
        public Dictionary<string, ShardPagingInfo> ShardPagingInfos { get; set; }

        public PagingInfo(IEnumerable<string> shardIds)
        {
            ShardPagingInfos = new Dictionary<string, ShardPagingInfo>();
            foreach (var shardId in shardIds)
            {
                ShardPagingInfos[shardId] = new ShardPagingInfo();
            }
        }

        public int GetLastInfo(string shardId, bool getNext)
        {
            var locations = ShardPagingInfos[shardId].PageLocations;
            return getNext ? locations[locations.Count - 1] : locations[locations.Count - 2];
        }
    }
}
