namespace RavenFS.Synchronization
{
	using RavenFS.Client;

	public static class SynchronizationUtils
	{
		public static SynchronizationReport SynchronizationExceptionReport(string filename, string exceptionMessage)
		{
			return new SynchronizationReport()
			{
				FileName = filename,
				Exception = new SynchronizationException(exceptionMessage)
			};
		}
	}
}