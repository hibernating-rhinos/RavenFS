namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityFileId
	{
		public byte[] FileId;   // m_FileId[32]
	}
}