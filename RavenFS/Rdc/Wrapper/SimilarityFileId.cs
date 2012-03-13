using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityFileId
	{
		public byte[] FileId;   // m_FileId[32]
	}
}