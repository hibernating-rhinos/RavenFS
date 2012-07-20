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
    public class IncomingCompletedSynchronizationTasksCollectionSource : VirtualCollectionSource<SynchronizationReport>
    {
        protected override Task<int> GetCount()
        {
            return GetResultAsync(0, 0).ContinueOnSuccess(r => r.TotalCount);
        }

        protected override Task<IList<SynchronizationReport>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return GetResultAsync(start, pageSize).ContinueOnSuccess(r => r.Items);
        }

        private Task<ListPage<SynchronizationReport>> GetResultAsync(int start, int pageSize)
        {
            return ApplicationModel.Current.Client.Synchronization
                .GetFinishedAsync(start, pageSize)
                .Catch();
        } 
    }
}
