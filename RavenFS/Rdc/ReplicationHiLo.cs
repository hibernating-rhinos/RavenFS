using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using RavenFS.Extensions;
using RavenFS.Storage;

namespace RavenFS.Rdc
{
    public class ReplicationHiLo
    {
        private long _currentLo = Capacity + 1;
        private readonly object _generatorLock = new object();
        private long _currentHi;
        private const long Capacity = 1024 * 16;

        private readonly TransactionalStorage _storage;

        public ReplicationHiLo(TransactionalStorage storage)
        {
            _storage = storage;
        }

        public long NextId()
        {
            var incrementedCurrentLow = Interlocked.Increment(ref _currentLo);
            if (incrementedCurrentLow > Capacity)
            {
                lock (_generatorLock)
                {
                    if (Thread.VolatileRead(ref _currentLo) > Capacity)
                    {
                        _currentHi = GetNextHi();
                        _currentLo = 1;
                        incrementedCurrentLow = 1;
                    }
                }
            }
            return (_currentHi - 1) * Capacity + (incrementedCurrentLow);
        }

        private long GetNextHi()
        {
            long result = 0;
            _storage.Batch(
                accessor =>
                {
                    accessor.TryGetConfigurationValue(ReplicationConstants.RavenReplicationVersionHiLo, out result);
                    result++;
                    accessor.SetConfigurationValue(ReplicationConstants.RavenReplicationVersionHiLo, result);
                });
            return result;
        }
    }
}