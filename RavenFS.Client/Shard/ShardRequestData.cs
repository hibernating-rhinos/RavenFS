using System;
using System.Collections.Generic;

namespace RavenFS.Client.Shard
{
	/// <summary>
	/// Information required to resolve the appropriate shard for an entity / entity and key
	/// </summary>
	public class ShardRequestData
	{
		/// <summary>
		/// Gets or sets the key.
		/// </summary>
		/// <value>The key.</value>
		public IList<string> Keys { get; set; }

		/// <summary>
		/// Gets or sets the type of the entity.
		/// </summary>
		/// <value>The type of the entity.</value>
		public Type EntityType { get; set; }

		public ShardRequestData()
		{
			Keys = new List<string>();
		}
	}
}