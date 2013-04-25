using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityMappedViewInfo
	{
		public string Data;
		public uint Length;
	}
}