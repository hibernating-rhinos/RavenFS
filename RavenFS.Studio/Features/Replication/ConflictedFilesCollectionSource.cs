using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Features.Replication
{
    public class ConflictedFilesCollectionSource : VirtualCollectionSource<ConflictItem>
    {
        protected override Task<IList<ConflictItem>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.Synchronization
                                   .GetConflictsAsync(start, pageSize)
                                   .Catch("Could not fetch list of conflicts from server").ContinueOnSuccess(r =>
                                   {
                                       SetCount(r.TotalCount);
                                       return r.Items;
                                   });
        }
    }
}
