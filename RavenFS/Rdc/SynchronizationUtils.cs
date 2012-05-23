namespace RavenFS.Rdc
{
	using System.Threading.Tasks;
	using Client;
	using Infrastructure;

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