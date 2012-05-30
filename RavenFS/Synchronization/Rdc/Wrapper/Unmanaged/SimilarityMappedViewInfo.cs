namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityMappedViewInfo
	{
		public string Data;
		public uint Length;
	}
}