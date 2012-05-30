namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct FindSimilarFileIndexResults
	{
		public uint FileIndex;
		public uint MatchCount;
	}
}