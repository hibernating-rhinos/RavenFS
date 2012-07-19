namespace RavenFS.Synchronization.Rdc.Wrapper.Unmanaged
{
	using System.Runtime.InteropServices;
	using System.Security.Permissions;

	[ClassInterface(ClassInterfaceType.None)]
	[Guid("96236A8B-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
	internal class RdcComparator { }
}