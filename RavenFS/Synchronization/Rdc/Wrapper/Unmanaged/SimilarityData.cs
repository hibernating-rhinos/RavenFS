namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityData
	{
		public char[] Data;     // m_Data[16]
	}
}