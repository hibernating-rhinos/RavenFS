namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct SimilarityDumpData
	{
		public uint FileIndex;
		public SimilarityData Data;
	}
}