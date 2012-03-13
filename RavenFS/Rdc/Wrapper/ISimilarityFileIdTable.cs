using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7F-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarityFileIdTable
	{
		Int32 CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
		                    IntPtr securityDescriptor, uint recordSize, out RdcCreatedTables isNew);

		Int32 CreateTableIndirect(IRdcFileWriter fileIdFile, bool truncate, uint recordSize, out RdcCreatedTables isNew);

		Int32 CloseTable(bool isValid);

		Int32 Append(SimilarityFileId similarityFileId, out uint similarityFileIndex);

		Int32 Lookup(uint similarityFileIndex, out SimilarityFileId similarityFileId);

		Int32 Invalidate(uint similarityFileIndex);

		Int32 GetRecordCount(out uint recordCount);

	}
}