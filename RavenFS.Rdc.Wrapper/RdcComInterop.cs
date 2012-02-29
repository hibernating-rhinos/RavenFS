using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Permissions;

using HRESULT = System.Int32;

namespace Rdc.Wrapper
{
    /// <summary>
    /// Internal MSRDC definitions
    /// </summary>
    public struct Msrdc
    {
        public const uint Version = 0x010000;
        public const uint MinimumCompatibleAppVersion = 0x010000;
        public const uint MinimumDepth = 1;
        public const uint MaximumDepth = 8;
        public const uint MinimumComparebuffer = 100000;
        public const uint MaximumComparebuffer = (1 << 30);
        public const uint DefaultComparebuffer = 3200000;
        public const uint MinimumInputbuffersize = 1024;
        public const uint MinimumHorizonsize = 128;
        public const uint MaximumHorizonsize = 1024 * 16;
        public const uint MinimumHashwindowsize = 2;
        public const uint MaximumHashwindowsize = 96;
        public const uint DefaultHashwindowsize1 = 48;
        public const uint DefaultHorizonsize1 = 1024;
        public const uint DefaultHashwindowsizeN = 2;
        public const uint DefaultHorizonsizeN = 128;
        public const uint MaximumTraitvalue = 63;
        public const uint MinimumMatchesrequired = 1;
        public const uint MaximumMatchesrequired = 16;
    }

    #region Enums
    internal enum GeneratorParametersType
    {
        Unused = 0,
        FilterMax = 1
    }

    public enum RdcNeedType
    {
        Source = 0,
        Target,
        Seed,
        SeedMax = 255
    }

    internal enum RdcCreatedTables
    {
        InvalidOrUnknown = 0,
        Existing,
        New
    }

    internal enum RdcMappingAccessMode
    {
        Undefined = 0,
        ReadOnly,
        ReadWrite
    }

    public enum RdcError : uint
    {
        NoError = 0,
        HeaderVersionNewer,
        HeaderVersionOlder,
        HeaderMissingOrCorrupt,
        HeaderWrongType,
        DataMissingOrCorrupt,
        DataTooManyRecords,
        FileChecksumMismatch,
        ApplicationError,
        Aborted,
        Win32Error
    }

    #endregion

    #region RDC Structures

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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RdcBufferPointer
    {
        public uint Size;
        public uint Used;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct RdcNeedPointer
    {
        public uint Size;
        public uint Used;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct RdcSignature
    {
        public IntPtr Signature;
        public ushort BlockLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct RdcSignaturePointer
    {
        public uint Size;
        public uint Used;
        [MarshalAs(UnmanagedType.Struct)]
        public RdcSignature Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SimilarityMappedViewInfo
    {
        public string Data;
        public uint Length;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SimilarityData
    {
        public char[] Data;     // m_Data[16]
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct FindSimilarFileIndexResults
    {
        public uint FileIndex;
        public uint MatchCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SimilarityDumpData
    {
        public uint FileIndex;
        public SimilarityData Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SimilarityFileId
    {
        public byte[] FileId;   // m_FileId[32]
    }

    #endregion

    #region RdcLibrary

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A78-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcLibrary
    {
        HRESULT ComputeDefaultRecursionDepth(Int64 fileSize, out int depth);

        HRESULT CreateGeneratorParameters([In] GeneratorParametersType parametersType, uint level,
            [Out] out IRdcGeneratorParameters iGeneratorParameters);

        HRESULT OpenGeneratorParameters(uint size, IntPtr parametersBlob,
            [Out] out IRdcGeneratorParameters iGeneratorParameters);

        HRESULT CreateGenerator(uint depth, [In] [MarshalAs(UnmanagedType.LPArray)] IRdcGeneratorParameters[] iGeneratorParametersArray,
            [Out] [MarshalAs(UnmanagedType.Interface)] out IRdcGenerator iGenerator);

        HRESULT CreateComparator([In, MarshalAs(UnmanagedType.Interface)] IRdcFileReader iSeedSignatureFiles, uint comparatorBufferSize,
            [Out, MarshalAs(UnmanagedType.Interface)] out IRdcComparator iComparator);

        HRESULT CreateSignatureReader([In, MarshalAs(UnmanagedType.Interface)] IRdcFileReader iFileReader, [Out] out IRdcSignatureReader iSignatureReader);

        HRESULT GetRDCVersion([Out] out uint currentVersion, [Out] out uint minimumCompatibileAppVersion);

    }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A85-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcLibrary { }

    #endregion

    #region RdcSimilarityGenerator
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A80-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcSimilarityGenerator
    {
        HRESULT EnableSimilarity();

        HRESULT Results(out SimilarityData similarityData);
    }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A92-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcSimilarityGenerator { }

    #endregion

    #region RDC COM Interfaces

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A71-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcGeneratorParameters
    {
        HRESULT GetGeneratorParametersType([Out] out GeneratorParametersType parametersType);

        HRESULT GetParametersVersion([Out] out uint currentVersion, [Out] out uint minimumCompatabileAppVersion);

        HRESULT GetSerializeSize([Out] out uint size);

        HRESULT Serialize(uint size, [Out] out IntPtr parametersBlob, [Out] out uint bytesWritten);
    }


    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A72-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcGeneratorFilterMaxParameters
    {
        HRESULT GetHorizonSize(out uint horizonSize);

        HRESULT SetHorizonSize(uint horizonSize);

        HRESULT GetHashWindowSize(out uint hashWindowSize);

        HRESULT SetHashWindowSize(uint hashWindowSize);
    }


    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A73-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcGenerator
    {
        HRESULT GetGeneratorParameters(uint level, [Out] out IRdcGeneratorParameters iGeneratorParameters);

        [PreserveSig]
        HRESULT Process([In, MarshalAs(UnmanagedType.U1)] bool endOfInput, [In, Out, MarshalAs(UnmanagedType.U1)] ref bool endOfOutput, [In, Out] ref RdcBufferPointer inputBuffer,
             [In] uint depth, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] outputBuffers, [Out] out RdcError errorCode);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A74-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcFileReader
    {
        void GetFileSize([Out] out UInt64 fileSize);

        void Read([In] UInt64 offsetFileStart, uint bytesToRead, [In, Out] ref uint bytesRead,
             [In] IntPtr buffer, [In, Out] ref bool eof);

        void GetFilePosition(out UInt64 offsetFromStart);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A75-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcFileWriter   // inherits IRdcFileReader
    {
        void Write(UInt64 offsetFileStart, uint bytesToWrite, [In, Out] ref IntPtr buffer);

        void Truncate();

        void DeleteOnClose();
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A76-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IRdcSignatureReader
    {
        HRESULT ReaderHeader(out RdcError errorCode);

        HRESULT ReadSignatures(
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref RdcSignaturePointer rdcSignaturePointer,
            [In, Out, MarshalAs(UnmanagedType.U1)] ref bool endOfOutput);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A77-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]    
    internal interface IRdcComparator
    {
        [PreserveSig]
        HRESULT Process([In, MarshalAs(UnmanagedType.Bool)] bool endOfInput, [In, Out, MarshalAs(UnmanagedType.Bool)] ref bool endOfOutput, [In, Out] ref RdcBufferPointer inputBuffer,
            [In, Out] ref RdcNeedPointer outputBuffer, out RdcError errorCode);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7A-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityReportProgress
    {
        HRESULT ReportProgress(uint percentCompleted);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7B-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityTableDumpState
    {
        HRESULT GetNextData(uint resultsSize, out uint resultsUsed, out bool eof, ref SimilarityDumpData results);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7C-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityTraitsMappedView
    {
        HRESULT Flush();

        HRESULT Unmap();

        HRESULT Get(UInt64 index, bool dirty, uint numElements, out SimilarityMappedViewInfo viewInfo);

        HRESULT GetView(out string mappedPateBegin, out string mappedPageEnd);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7D-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityTraitsMapping
    {
        HRESULT CloseMapping();

        HRESULT SetFileSize(UInt64 fileSize);

        HRESULT GetFileSize(out UInt64 fileSize);

        HRESULT OpenMapping(RdcMappingAccessMode accessMode, UInt64 begin, UInt64 end, out UInt64 actualEnd);

        HRESULT ResizeMapping(RdcMappingAccessMode accessMode, UInt64 begin, UInt64 end, out UInt64 actualEnd);

        HRESULT GetPageSize(out uint pageSize);

        HRESULT CreateView(uint minimumMappedPages, RdcMappingAccessMode accessMode,
            out ISimilarityTraitsMappedView mappedView);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7E-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityTraitsTable
    {
        HRESULT CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
            IntPtr securityDescriptor, out RdcCreatedTables isNew);

        HRESULT CreateTableIndirect(ISimilarityTraitsMapping mapping, bool truncate, out RdcCreatedTables isNew);

        HRESULT CloseTable(bool isValid);

        HRESULT Append(SimilarityData data, uint fileIndex);

        HRESULT FindSimilarFileIndex(SimilarityData similarityData, ushort numberOfMatchesRequired,
            out FindSimilarFileIndexResults findSimilarFileIndexResults, uint resultsSize,
            out uint resultsUsed);

        HRESULT BeginDump(out ISimilarityTableDumpState similarityTableDumpState);

        HRESULT GetLastIndex(out uint fileIndex);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A7F-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarityFileIdTable
    {
        HRESULT CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
            IntPtr securityDescriptor, uint recordSize, out RdcCreatedTables isNew);

        HRESULT CreateTableIndirect(IRdcFileWriter fileIdFile, bool truncate, uint recordSize, out RdcCreatedTables isNew);

        HRESULT CloseTable(bool isValid);

        HRESULT Append(SimilarityFileId similarityFileId, out uint similarityFileIndex);

        HRESULT Lookup(uint similarityFileIndex, out SimilarityFileId similarityFileId);

        HRESULT Invalidate(uint similarityFileIndex);

        HRESULT GetRecordCount(out uint recordCount);

    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A81-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface IFindSimilarResults
    {
        HRESULT GetSize(out uint size);

        HRESULT GetNextFileId(out uint numTraitsMatched, out SimilarityFileId similarityFileId);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("96236A83-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    internal interface ISimilarity
    {
        HRESULT CreateTable([MarshalAs(UnmanagedType.LPWStr)] string path, bool truncate,
            IntPtr securityDescriptor, uint recordSize, out RdcCreatedTables isNew);

        HRESULT CreateTableIndirect(ISimilarityTraitsMappedView mapping, IRdcFileWriter fileIdFile,
            bool truncate, uint recordSize, out RdcCreatedTables isNew);

        HRESULT CloseTable(bool isValid);

        HRESULT Append(SimilarityFileId similarityFileId, SimilarityData similarityData);

        HRESULT FindSimilarFileId(SimilarityData similarityData, ushort numberOfMatchesRequired,
            uint resultsSize, out IFindSimilarResults findSimilarResults);

        HRESULT CopyAndSwap(ISimilarity newSimilarityTables, ISimilarityReportProgress reportProgress);

        HRESULT GetRecordCount(out uint recordCount);
    }

    #endregion

    #region COM Class Imports

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A86-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcGeneratorParameters { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A87-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcGeneratorFilterMaxParameters { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A88-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcGenerator { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A8A-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcSignatureReader { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A8B-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class RdcComparator { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A8D-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityReportProgress { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A8E-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityTableDumpState { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A8F-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityTraitsTable { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A90-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityFileIdTable { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A91-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class Similarity { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A93-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class FindSimilarResults { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A94-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityTraitsMapping { }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("96236A95-9DBC-11DA-9E3F-0011114AE311")]
    [ComImport()]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
    internal class SimilarityTraitsMappedView { }

    #endregion




}
