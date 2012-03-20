using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	/// <summary>
	/// Make sure we align on the 8 byte boundry.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct RdcNeed
	{
		public RdcNeedType BlockType;
		public UInt64 FileOffset;
		public UInt64 BlockLength;
	}
}