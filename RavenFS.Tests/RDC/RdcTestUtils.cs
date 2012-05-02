using System.Threading.Tasks;
using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
	using System;

	public class RdcTestUtils
    {
		public static SynchronizationReport SynchronizeAndWaitForStatus(RavenFileSystemClient sourceClient, string destinationUrl, string fileName, RavenFileSystemClient clientThatWaitsForStatus = null)
		{
			if (clientThatWaitsForStatus == null)
			{
				clientThatWaitsForStatus = sourceClient;
			}

			sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationUrl).Wait();
			var synchronizationReportTask = Task.Factory.StartNew(
				() =>
				{
					SynchronizationReport report;
					do
					{
						report = clientThatWaitsForStatus.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
					} while (report == null);
					return report;
				});
			return synchronizationReportTask.Result;
		}

		public static SynchronizationReport ResolveConflictAndSynchronize(RavenFileSystemClient sourceClient, RavenFileSystemClient destinationClient, string fileName)
        {
			try
			{
				sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Wait();
			}
			catch (Exception)
			{

			}

			destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.RemoteVersion).Wait();
			return sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Result;
        }
    }
}
