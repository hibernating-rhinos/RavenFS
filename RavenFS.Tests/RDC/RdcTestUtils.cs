using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
    public class RdcTestUtils
    {
        public static SynchronizationReport SynchronizeAndWaitForStatus(RavenFileSystemClient source, string sourceUrl, string fileName)
        {
			source.Synchronization.SynchronizeDestinationsAsync("test.txt").Wait();
            var synchronizationReportTask = Task.Factory.StartNew(
                () =>
                {
                    SynchronizationReport report;
                    do
                    {
                        report = source.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
                    } while (report == null);
                    return report;
                });
            return synchronizationReportTask.Result;
        }

        public static SynchronizationReport ResolveConflictAndSynchronize(string fileName, RavenFileSystemClient sourceClient)
        {
            SynchronizeAndWaitForStatus(sourceClient, sourceClient.ServerUrl, fileName);
            sourceClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.Theirs).Wait();
            return SynchronizeAndWaitForStatus(sourceClient, sourceClient.ServerUrl, fileName);
        }

		public static SynchronizationReport SynchronizeAndWaitForStatusOld(RavenFileSystemClient client, string sourceUrl, string fileName)
		{
			client.Synchronization.StartSynchronizationAsync(sourceUrl, fileName).Wait();
			var synchronizationReportTask = Task.Factory.StartNew(
				() =>
				{
					SynchronizationReport report;
					do
					{
						report = client.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
					} while (report == null);
					return report;
				});
			return synchronizationReportTask.Result;
		}

		public static SynchronizationReport ResolveConflictAndSynchronize(string fileName, RavenFileSystemClient client, RavenFileSystemClient sourceClient)
		{
			SynchronizeAndWaitForStatusOld(client, sourceClient.ServerUrl, fileName);
			client.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.Theirs).Wait();
			return SynchronizeAndWaitForStatusOld(client, sourceClient.ServerUrl, fileName);
		}
    }
}
