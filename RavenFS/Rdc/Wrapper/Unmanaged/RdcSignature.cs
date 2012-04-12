using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcSignature
	{
		public IntPtr Signature;
		public ushort BlockLength;
	}
}