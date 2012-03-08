using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityData
	{
		public char[] Data;     // m_Data[16]
	}
}