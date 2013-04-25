using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct FindSimilarFileIndexResults
	{
		public uint FileIndex;
		public uint MatchCount;
	}
}