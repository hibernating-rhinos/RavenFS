using System;
using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7C-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarityTraitsMappedView
	{
		Int32 Flush();

		Int32 Unmap();

		Int32 Get(UInt64 index, bool dirty, uint numElements, out SimilarityMappedViewInfo viewInfo);

		Int32 GetView(out string mappedPateBegin, out string mappedPageEnd);
	}
}