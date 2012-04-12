using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcSignaturePointer
	{
		public uint Size;
		public uint Used;
		[MarshalAs(UnmanagedType.Struct)]
		public RdcSignature Data;
	}
}