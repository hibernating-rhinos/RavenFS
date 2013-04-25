using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityFileId
	{
		public byte[] FileId; // m_FileId[32]
	}
}