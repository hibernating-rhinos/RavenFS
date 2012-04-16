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
        public static SynchronizationReport SynchronizeAndWaitForStatus(RavenFileSystemClient client, string sourceUrl, string fileName)
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
    }
}
