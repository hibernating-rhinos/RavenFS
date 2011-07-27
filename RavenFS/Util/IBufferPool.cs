namespace RavenFS.Util
{
	public interface IBufferPool
	{
		void Dispose();
		byte[] TakeBuffer(int size);
		void ReturnBuffer(byte[] buffer);
	}
}