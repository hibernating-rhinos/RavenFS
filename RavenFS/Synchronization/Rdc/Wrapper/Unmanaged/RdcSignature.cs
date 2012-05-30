namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System;
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcSignature
	{
		public IntPtr Signature;
		public ushort BlockLength;
	}
}