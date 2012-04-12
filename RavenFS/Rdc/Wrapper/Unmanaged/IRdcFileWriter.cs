using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A75-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface IRdcFileWriter   // inherits IRdcFileReader
	{
		void Write(UInt64 offsetFileStart, uint bytesToWrite, [In, Out] ref IntPtr buffer);

		void Truncate();

		void DeleteOnClose();
	}
}