using System;
using System.Reactive.Linq;
using System.Reactive;
using RavenFS.Client;
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

            ApplicationModel.Current.Client.Notifications.Notifications()
                .OfType<SynchronizationUpdate>()
                .Throttle(TimeSpan.FromSeconds(1))
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(notification =>
                               {
                                   if (notification.SynchronizationDirection == SynchronizationDirection.Outgoing)
                                   {
                                       OutgoingQueue.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing);
                                   }
                                   else
                                   {
                                       IncomingItems.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing);
                                   }
                               });
        }
    }
}
