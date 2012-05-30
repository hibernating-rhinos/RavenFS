namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System;
	using System.Runtime.InteropServices;

	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A78-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	public interface IRdcLibrary
	{
		Int32 ComputeDefaultRecursionDepth(Int64 fileSize, out int depth);

		Int32 CreateGeneratorParameters([In] GeneratorParametersType parametersType, uint level,
		                                  [Out] out IRdcGeneratorParameters iGeneratorParameters);

		Int32 OpenGeneratorParameters(uint size, IntPtr parametersBlob,
		                                [Out] out IRdcGeneratorParameters iGeneratorParameters);

		Int32 CreateGenerator(uint depth, [In] [MarshalAs(UnmanagedType.LPArray)] IRdcGeneratorParameters[] iGeneratorParametersArray,
		                        [Out] [MarshalAs(UnmanagedType.Interface)] out IRdcGenerator iGenerator);

		Int32 CreateComparator([In, MarshalAs(UnmanagedType.Interface)] IRdcFileReader iSeedSignatureFiles, uint comparatorBufferSize,
		                         [Out, MarshalAs(UnmanagedType.Interface)] out IRdcComparator iComparator);

		Int32 CreateSignatureReader([In, MarshalAs(UnmanagedType.Interface)] IRdcFileReader iFileReader, [Out] out IRdcSignatureReader iSignatureReader);

		Int32 GetRDCVersion([Out] out uint currentVersion, [Out] out uint minimumCompatibileAppVersion);

	}
}