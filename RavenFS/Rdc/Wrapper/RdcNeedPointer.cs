using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct RdcNeedPointer
	{
		public uint Size;
		public uint Used;
		public IntPtr Data;
	}
}