using RavenFS.Util;

namespace RavenFS.Storage
{
	public interface IStorage
	{
		HashKey InsertPage(byte[] buffer, int position, int size);
		void PutFile(string filename, long totalSize);
		void AssociatePage(string filename, HashKey pageKey);
	}
}