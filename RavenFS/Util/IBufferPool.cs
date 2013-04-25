using System;

namespace RavenFS.Util
{
	public interface IBufferPool : IDisposable
	{
		byte[] TakeBuffer(int size);
		void ReturnBuffer(byte[] buffer);
	}
}