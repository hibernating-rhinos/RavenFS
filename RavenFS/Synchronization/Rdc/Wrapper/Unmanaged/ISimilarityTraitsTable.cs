namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System;
	using System.Runtime.InteropServices;

	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7E-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface ISimilarityTraitsTable
	{
		Int32 CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
		                    IntPtr securityDescriptor, out RdcCreatedTables isNew);

		Int32 CreateTableIndirect(ISimilarityTraitsMapping mapping, bool truncate, out RdcCreatedTables isNew);

		Int32 CloseTable(bool isValid);

		Int32 Append(SimilarityData data, uint fileIndex);

		Int32 FindSimilarFileIndex(SimilarityData similarityData, ushort numberOfMatchesRequired,
		                             out FindSimilarFileIndexResults findSimilarFileIndexResults, uint resultsSize,
		                             out uint resultsUsed);

		Int32 BeginDump(out ISimilarityTableDumpState similarityTableDumpState);

		Int32 GetLastIndex(out uint fileIndex);
	}
}