using System;
using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7D-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarityTraitsMapping
	{
		Int32 CloseMapping();

		Int32 SetFileSize(UInt64 fileSize);

		Int32 GetFileSize(out UInt64 fileSize);

		Int32 OpenMapping(RdcMappingAccessMode accessMode, UInt64 begin, UInt64 end, out UInt64 actualEnd);

		Int32 ResizeMapping(RdcMappingAccessMode accessMode, UInt64 begin, UInt64 end, out UInt64 actualEnd);

		Int32 GetPageSize(out uint pageSize);

		Int32 CreateView(uint minimumMappedPages, RdcMappingAccessMode accessMode,
		                 out ISimilarityTraitsMappedView mappedView);
	}
}