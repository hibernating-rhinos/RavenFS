using System.Threading.Tasks;
using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
    public class RdcTestUtils
    {
		//public static SynchronizationReport SynchronizeAndWaitForStatus(RavenFileSystemClient sourceClient, RavenFileSystemClient destinationClient, string fileName)
		//{
		//    sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Wait();
		//    var synchronizationReportTask = Task.Factory.StartNew(
		//        () =>
		//        {
		//            SynchronizationReport report;
		//            do
		//            {
		//                report = destinationClient.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
		//            } while (report == null);
		//            return report;
		//        });
		//    return synchronizationReportTask.Result;
		//}

		//public static SynchronizationReport SynchronizeAndWaitForStatusFromSource(RavenFileSystemClient sourceClient, RavenFileSystemClient destinationClient, string fileName)
		//{
		//    sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Wait();
		//    var synchronizationReportTask = Task.Factory.StartNew(
		//        () =>
		//        {
		//            SynchronizationReport report;
		//            do
		//            {
		//                report = sourceClient.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
		//            } while (report == null);
		//            return report;
		//        });
		//    return synchronizationReportTask.Result;
		//}

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
			SynchronizeAndWaitForStatus(sourceClient, destinationClient.ServerUrl, fileName);
			destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.RemoteVersion).Wait();
			return SynchronizeAndWaitForStatus(sourceClient, destinationClient.ServerUrl, fileName, destinationClient);
        }

		//public static SynchronizationReport SynchronizeAndWaitForStatusOld(RavenFileSystemClient client, string sourceUrl, string fileName)
		//{
		//    client.Synchronization.StartSynchronizationAsync(sourceUrl, fileName).Wait();
		//    var synchronizationReportTask = Task.Factory.StartNew(
		//        () =>
		//        {
		//            SynchronizationReport report;
		//            do
		//            {
		//                report = client.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
		//            } while (report == null);
		//            return report;
		//        });
		//    return synchronizationReportTask.Result;
		//}

		//public static SynchronizationReport ResolveConflictAndSynchronize(string fileName, RavenFileSystemClient client, RavenFileSystemClient sourceClient)
		//{
		//    SynchronizeAndWaitForStatusOld(client, sourceClient.ServerUrl, fileName);
		//    client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.Theirs).Wait();
		//    return SynchronizeAndWaitForStatusOld(client, sourceClient.ServerUrl, fileName);
		//}
    }
}
