using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;

namespace RavenFS.Util
{
	public class BufferPool : IBufferPool
	{
		public class BufferTracker
		{
			private StackTrace stackTrace;

			public void TrackAllocation()
			{
				stackTrace = new StackTrace(true);
				GC.ReRegisterForFinalize(this);
			}

			public void Discard()
			{
				stackTrace = null;
				GC.SuppressFinalize(this);
			}

			~BufferTracker()
			{
				if (stackTrace == null)
					return;

				throw new InvalidOperationException(
					"A buffer was leaked. Initial allocation:" + Environment.NewLine + stackTrace
					);
			}
		}

		private readonly BufferManager bufferManager;
		private ConditionalWeakTable<byte[], BufferTracker> trackLeakedBuffers = new ConditionalWeakTable<byte[], BufferTracker>();

		public BufferPool(long maxBufferPoolSize, int maxBufferSize)
		{
			bufferManager = BufferManager.CreateBufferManager(maxBufferPoolSize, maxBufferSize);
		}

		public void Dispose()
		{
			bufferManager.Clear();
			// note that disposing the pool before returning all of the buffers will cause a crash
		}

		public byte[] TakeBuffer(int size)
		{
			var buffer = bufferManager.TakeBuffer(size);
			trackLeakedBuffers.GetOrCreateValue(buffer).TrackAllocation();
			return buffer;
		}

		public void ReturnBuffer(byte[] buffer)
		{
			BufferTracker value;
			if (trackLeakedBuffers.TryGetValue(buffer, out value))
				value.Discard();

			bufferManager.ReturnBuffer(buffer);
		}
	}
}