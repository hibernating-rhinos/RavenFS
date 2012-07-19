namespace RavenFS.Synchronization
{
	using System.Threading.Tasks;
	using RavenFS.Client;
	using RavenFS.Infrastructure;

	public static class SynchronizationUtils
	{
		public static Task<SynchronizationReport> SynchronizationExceptionReport(string filename, string exceptionMessage)
		{
			return new CompletedTask<SynchronizationReport>(new SynchronizationReport()
			{
				FileName = filename,
				Exception = new SynchronizationException(exceptionMessage)
			});
		}
	}
}