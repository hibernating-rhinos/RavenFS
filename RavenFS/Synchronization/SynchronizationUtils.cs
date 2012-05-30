namespace RavenFS.Synchronization
{
	using System.Threading.Tasks;
	using RavenFS.Client;
	using RavenFS.Infrastructure;

	public static class SynchronizationUtils
	{
		public static Task<SynchronizationReport> SynchronizationExceptionReport(string exceptionMessage)
		{
			return new CompletedTask<SynchronizationReport>(new SynchronizationReport()
			{
				Exception = new SynchronizationException(exceptionMessage)
			});
		}
	}
}