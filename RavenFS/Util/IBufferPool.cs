namespace RavenFS.Util
{
	using System;

	public interface IBufferPool : IDisposable
	{
		byte[] TakeBuffer(int size);
		void ReturnBuffer(byte[] buffer);
	}
}