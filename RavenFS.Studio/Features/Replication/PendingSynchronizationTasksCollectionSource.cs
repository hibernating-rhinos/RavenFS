using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Features.Replication
{
    public class PendingSynchronizationTasksCollectionSource : VirtualCollectionSource<SynchronizationDetails>
    {
        protected override Task<IList<SynchronizationDetails>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.Synchronization
                                   .GetPendingAsync(start, pageSize)
                                   .Catch("Could not get pending tasks from server").ContinueOnSuccess(r =>
                                       {
                                           SetCount(r.TotalCount);
                                           return r.Items;
                                       });
        }
    }
}
