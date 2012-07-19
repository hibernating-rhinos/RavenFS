namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System;
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcNeedPointer
	{
		public uint Size;
		public uint Used;
		public IntPtr Data;
	}
}