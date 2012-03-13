namespace RavenFS.Rdc.Wrapper
{
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
}