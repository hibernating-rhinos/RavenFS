using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityMappedViewInfo
	{
		public string Data;
		public uint Length;
	}
}