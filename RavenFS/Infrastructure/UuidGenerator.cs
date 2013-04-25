using System;
using System.Threading;
using RavenFS.Extensions;

namespace RavenFS.Infrastructure
{
	public class UuidGenerator
	{
		private readonly long _currentEtagBase;
		private readonly SequenceActions _sequenceActions;
		private long _sequentialUuidCounter;

		public UuidGenerator(SequenceActions sequenceActions)
		{
			_sequenceActions = sequenceActions;
			_currentEtagBase = sequenceActions.GetNextValue("Raven/Etag");
		}

		public Guid CreateSequentialUuid()
		{
			var ticksAsBytes = BitConverter.GetBytes(_currentEtagBase);
			Array.Reverse(ticksAsBytes);
			var increment = Interlocked.Increment(ref _sequentialUuidCounter);
			var currentAsBytes = BitConverter.GetBytes(increment);
			Array.Reverse(currentAsBytes);
			var bytes = new byte[16];
			Array.Copy(ticksAsBytes, 0, bytes, 0, ticksAsBytes.Length);
			Array.Copy(currentAsBytes, 0, bytes, 8, currentAsBytes.Length);
			return bytes.TransfromToGuidWithProperSorting();
		}
	}
}