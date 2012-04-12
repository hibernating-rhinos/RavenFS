using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7B-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarityTableDumpState
	{
		Int32 GetNextData(uint resultsSize, out uint resultsUsed, out bool eof, ref SimilarityDumpData results);
	}
}