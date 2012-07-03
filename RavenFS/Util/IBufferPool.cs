namespace RavenFS.Util
{
	using System;

	public interface IBufferPool : IDisposable
	{
		void Dispose();
		byte[] TakeBuffer(int size);
		void ReturnBuffer(byte[] buffer);
	}
}