using System;
using System.Runtime.InteropServices;

namespace RavenFS.Rdc.Wrapper
{
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("96236A7A-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	public interface ISimilarityReportProgress
	{
		Int32 ReportProgress(uint percentCompleted);
	}
}