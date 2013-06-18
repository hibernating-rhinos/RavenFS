using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenFS.Client.Shard
{
	/// <summary>
	/// Implementers of this interface provide a way to decide which shards will be queried
	/// for a specified operation
	/// </summary>
	public interface IShardResolutionStrategy
	{
		/// <summary>
		///  Generate a shard id for the specified entity
		///  </summary>
		string GenerateShardIdFor(object entity, RavenFileSystemClient client);

		/// <summary>
		///  The shard id for the server that contains the metadata (such as the HiLo documents)
		///  for the given entity
		///  </summary>
		string MetadataShardIdFor(object entity);

		/// <summary>
		///  Selects the shard ids appropriate for the specified data.
		///  </summary><returns>Return a list of shards ids that will be search. Returning null means search all shards.</returns>
		IList<string> PotentialShardsFor(ShardRequestData requestData);
	}
}
