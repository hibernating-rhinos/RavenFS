﻿using System.Threading;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Storage;

namespace RavenFS.Synchronization
{
	public class SynchronizationHiLo
	{
		private const long Capacity = 1024*16;
		private readonly object generatorLock = new object();

		private readonly TransactionalStorage storage;
		private long currentHi;
		private long currentLo = Capacity + 1;

		public SynchronizationHiLo(TransactionalStorage storage)
		{
			this.storage = storage;
		}

		public long NextId()
		{
			var incrementedCurrentLow = Interlocked.Increment(ref currentLo);
			if (incrementedCurrentLow > Capacity)
			{
				lock (generatorLock)
				{
					if (Thread.VolatileRead(ref currentLo) > Capacity)
					{
						currentHi = GetNextHi();
						currentLo = 1;
						incrementedCurrentLow = 1;
					}
				}
			}
			return (currentHi - 1)*Capacity + (incrementedCurrentLow);
		}

		private long GetNextHi()
		{
			long result = 0;
			storage.Batch(
				accessor =>
					{
						accessor.TryGetConfigurationValue(SynchronizationConstants.RavenSynchronizationVersionHiLo, out result);
						result++;
						accessor.SetConfigurationValue(SynchronizationConstants.RavenSynchronizationVersionHiLo, result);
					});
			return result;
		}
	}
}