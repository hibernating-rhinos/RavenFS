using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using RavenFS.Client.Connections;

namespace RavenFS.Client.Shard
{
	/// <summary>
	/// Default shard strategy for the sharding document store
	/// </summary>
	public class ShardStrategy
	{
		private readonly IDictionary<string, RavenFileSystemClient> shards;

		public delegate string ModifyDocumentIdFunc(FileConvention convention, string shardId, string documentId);

		public ShardStrategy(IDictionary<string, RavenFileSystemClient> shards)
		{
			if (shards == null) throw new ArgumentNullException("shards");
			if (shards.Count == 0)
				throw new ArgumentException("Shards collection must have at least one item", "shards");

			this.shards = new Dictionary<string, RavenFileSystemClient>(shards, StringComparer.OrdinalIgnoreCase);


			Conventions = shards.First().Value.Convention.Clone();

			ShardAccessStrategy = new SequentialShardAccessStrategy();
			ShardResolutionStrategy = new DefaultShardResolutionStrategy(shards.Keys, this);
			ModifyDocumentId = (convention, shardId, documentId) => shardId + convention.IdentityPartsSeparator + documentId;
		}

		public FileConvention Conventions { get; set; }

		/// <summary>
		/// Gets or sets the shard resolution strategy.
		/// </summary>
		public IShardResolutionStrategy ShardResolutionStrategy { get; set; }

		/// <summary>
		/// Gets or sets the shard access strategy.
		/// </summary>
		public IShardAccessStrategy ShardAccessStrategy { get; set; }

		/// <summary>
		/// Get or sets the modification for the document id for sharding
		/// </summary>
		public ModifyDocumentIdFunc ModifyDocumentId { get; set; }

		public IDictionary<string, RavenFileSystemClient> Shards
		{
			get { return shards; }
		}

		/// <summary>
		/// Instructs the sharding strategy to shard the <typeparamref name="TEntity"/> instances based on 
		/// round robin strategy.
		/// </summary>
		public ShardStrategy ShardingOn<TEntity>()
		{
			var defaultShardResolutionStrategy = ShardResolutionStrategy as DefaultShardResolutionStrategy;
			if (defaultShardResolutionStrategy == null)
				throw new NotSupportedException("ShardingOn<T> is only supported if ShardResolutionStrategy is DefaultShardResolutionStrategy");

			var identityProperty = Conventions.GetIdentityProperty(typeof(TEntity));
			if (identityProperty == null)
				throw new ArgumentException("Cannot set default sharding on " + typeof(TEntity) +
											" because RavenDB was unable to figure out what the identity property of this entity is.");

			var parameterExpression = Expression.Parameter(typeof(TEntity), "p1");
			var lambdaExpression = Expression.Lambda<Func<TEntity, object>>(Expression.MakeMemberAccess(parameterExpression, identityProperty), parameterExpression);

			defaultShardResolutionStrategy.ShardingOn(lambdaExpression, valueTranslator: result =>
			{
				if (ReferenceEquals(result, null))
					throw new InvalidOperationException("Got null for the shard id in the value translator for " +
														typeof(TEntity) + " using " + lambdaExpression +
														", no idea how to get the shard id from null.");

				var shardNum = Math.Abs(StableHashString(result.ToString())) % shards.Count;
				return shards.ElementAt(shardNum).Key;
			});
			return this;
		}


		public int StableHashString(string text)
		{
			unchecked
			{
				return text.ToCharArray().Aggregate(11, (current, c) => current * 397 + c);
			}
		}

		/// <summary>
		/// Instructs the sharding strategy to shard the <typeparamref name="TEntity"/> instances based on 
		/// the property specified in <paramref name="shardingProperty"/>, with an optional translation to
		/// the shard id.
		/// </summary>
		public ShardStrategy ShardingOn<TEntity>(Expression<Func<TEntity, string>> shardingProperty,
			Func<string, string> translator = null
		)
		{
			var defaultShardResolutionStrategy = ShardResolutionStrategy as DefaultShardResolutionStrategy;
			if (defaultShardResolutionStrategy == null)
				throw new NotSupportedException("ShardingOn<T> is only supported if ShardResolutionStrategy is DefaultShardResolutionStrategy");

			defaultShardResolutionStrategy.ShardingOn(shardingProperty, translator);
			return this;
		}

		/// <summary>
		/// Instructs the sharding strategy to shard the <typeparamref name="TEntity"/> instances based on 
		/// the property specified in <paramref name="shardingProperty"/>, with an optional translation of the value
		/// from a non string representation to a string and from a string to the shard id.
		/// </summary>
		public ShardStrategy ShardingOn<TEntity, TResult>(Expression<Func<TEntity, TResult>> shardingProperty,
			Func<TResult, string> valueTranslator = null,
			Func<string, string> queryTranslator = null
			)
		{
			var defaultShardResolutionStrategy = ShardResolutionStrategy as DefaultShardResolutionStrategy;
			if (defaultShardResolutionStrategy == null)
				throw new NotSupportedException("ShardingOn<T> is only supported if ShardResolutionStrategy is DefaultShardResolutionStrategy");

			defaultShardResolutionStrategy.ShardingOn(shardingProperty, valueTranslator, queryTranslator);
			return this;
		}
	}
}