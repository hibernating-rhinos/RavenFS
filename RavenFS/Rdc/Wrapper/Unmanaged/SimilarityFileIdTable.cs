using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace RavenFS.Rdc.Wrapper.Unmanaged
{
	[ClassInterface(ClassInterfaceType.None)]
	[Guid("96236A90-9DBC-11DA-9E3F-0011114AE311")]
	[ComImport]
	[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
	internal class SimilarityFileIdTable { }
}