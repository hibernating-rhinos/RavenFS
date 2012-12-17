using System;
using System.Reactive.Linq;
using System.Reactive;
using RavenFS.Client;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Features.Replication;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class SynchronizationActivityModel : ViewModel
    {
        public SynchronizationActivityModel()
        {
            OutgoingQueue = new VirtualCollection<SynchronizationDetails>(new OutgoingSynchronizationQueueCollectionSource(), 25, 3, 
                new KeysComparer<SynchronizationDetails>(d => d.DestinationUrl + "/" + d.FileName));
            IncomingItems =
                new VirtualCollection<SynchronizationReport>(
                    new IncomingCompletedSynchronizationTasksCollectionSource(), 25, 4);
        }

        public VirtualCollection<SynchronizationReport> IncomingItems { get; private set; }

        public VirtualCollection<SynchronizationDetails> OutgoingQueue { get; private set; }

        protected override void OnViewLoaded()
        {
            OutgoingQueue.Refresh();
            IncomingItems.Refresh();

            ApplicationModel.Current.Client.Notifications.SynchronizationUpdates()
                .Where(notification => notification.SynchronizationDirection == SynchronizationDirection.Outgoing)
                .SampleResponsive(TimeSpan.FromSeconds(5.0))
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(notification => OutgoingQueue.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));

            ApplicationModel.Current.Client.Notifications.SynchronizationUpdates()
                .Where(notification => notification.SynchronizationDirection == SynchronizationDirection.Incoming)
                .SampleResponsive(TimeSpan.FromSeconds(5.0))
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(notification => IncomingItems.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));
        }
    }
}
