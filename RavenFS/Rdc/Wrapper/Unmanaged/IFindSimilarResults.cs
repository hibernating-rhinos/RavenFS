using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A81-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	internal interface IFindSimilarResults
	{
		Int32 GetSize(out uint size);

		Int32 GetNextFileId(out uint numTraitsMatched, out SimilarityFileId similarityFileId);
	}
}