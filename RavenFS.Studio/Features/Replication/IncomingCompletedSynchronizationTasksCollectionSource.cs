using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Features.Replication
{
    public class IncomingCompletedSynchronizationTasksCollectionSource : VirtualCollectionSource<SynchronizationReport>
    {

        protected override Task<IList<SynchronizationReport>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.Synchronization
                                   .GetFinishedAsync(start, pageSize)
                                   .Catch("Could not fetch completed tasks from server").ContinueOnSuccess(r =>
                                       {
                                           SetCount(r.TotalCount);
                                           return r.Items;
                                       });
        }
    }
}
