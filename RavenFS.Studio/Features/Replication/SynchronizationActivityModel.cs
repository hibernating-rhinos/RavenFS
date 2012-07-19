using System;
using System.Net;
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
using System.Reactive.Linq;

namespace RavenFS.Studio.Features.Replication
{
    public class SynchronizationActivityModel : ViewModel
    {
        public SynchronizationActivityModel()
        {
            Queue = new VirtualCollection<SynchronizationDetails>(new SynchronizationQueueCollectionSource(), 25, 3, 
                new KeysComparer<SynchronizationDetails>(d => d.DestinationUrl + "/" + d.FileName));
        }

        public VirtualCollection<SynchronizationDetails> Queue { get; private set; }

        protected override void OnViewLoaded()
        {
            Queue.Refresh();

            ApplicationModel.Current.Client.Notifications
                .SynchronizationUpdates(SynchronizationDirection.Outgoing)
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(_ => Queue.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));
        }
    }
}
