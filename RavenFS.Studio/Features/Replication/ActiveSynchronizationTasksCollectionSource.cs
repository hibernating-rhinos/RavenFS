using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Features.Replication
{
    public class ActiveSynchronizationTasksCollectionSource : VirtualCollectionSource<SynchronizationDetails>
    {
        protected override Task<IList<SynchronizationDetails>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.Synchronization
                                   .GetActiveAsync(start, pageSize)
                                   .Catch("Could not fetch list of synchronization tasks from server")
                                   .ContinueOnSuccess(r =>
                                       {
                                           SetCount(r.TotalCount);
                                           return r.Items;
                                       });
        }
    }
}
