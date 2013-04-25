using System;
using System.Runtime.InteropServices;

namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A83-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarity
	{
		Int32 CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
		                  IntPtr securityDescriptor, uint recordSize, out RdcCreatedTables isNew);

		Int32 CreateTableIndirect(ISimilarityTraitsMappedView mapping, IRdcFileWriter fileIdFile,
		                          bool truncate, uint recordSize, out RdcCreatedTables isNew);

		Int32 CloseTable(bool isValid);

		Int32 Append(SimilarityFileId similarityFileId, SimilarityData similarityData);

		Int32 FindSimilarFileId(SimilarityData similarityData, ushort numberOfMatchesRequired,
		                        uint resultsSize, out IFindSimilarResults findSimilarResults);

		Int32 CopyAndSwap(ISimilarity newSimilarityTables, ISimilarityReportProgress reportProgress);

		Int32 GetRecordCount(out uint recordCount);
	}
}