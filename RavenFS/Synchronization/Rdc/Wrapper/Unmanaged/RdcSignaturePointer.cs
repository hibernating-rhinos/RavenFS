namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcSignaturePointer
	{
		public uint Size;
		public uint Used;
		[MarshalAs(UnmanagedType.Struct)]
		public RdcSignature Data;
	}
}