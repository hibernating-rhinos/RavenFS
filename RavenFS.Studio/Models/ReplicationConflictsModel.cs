using System;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Features.Replication;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class ReplicationConflictsModel : ViewModel
    {
        private ICommand _resolveWithLocalCommand;
        private ICommand _resolveWithRemoteCommand;
        public VirtualCollection<ConflictItem> ConflictedFiles { get; private set; }

        public ReplicationConflictsModel()
        {
            ConflictedFiles =
                new VirtualCollection<ConflictItem>(new ConflictedFilesCollectionSource(), 30, 30);

            SelectedItems = new ItemSelection<VirtualItem<ConflictItem>>();
        }

        public ICommand ResolveWithLocalVersionCommand
        {
            get
            {
                return _resolveWithLocalCommand ??
                       (_resolveWithLocalCommand = new ResolveConflictWithLocalVersionCommand(SelectedItems));
            }
        }

        public ICommand ResolveWithRemoteVersionCommand
        {
            get
            {
                return _resolveWithRemoteCommand ??
                       (_resolveWithRemoteCommand = new ResolveConflictWithRemoteVersionCommand(SelectedItems));
            }
        }

        public ItemSelection<VirtualItem<ConflictItem>> SelectedItems { get; private set; }
 
        protected override void OnViewLoaded()
        {
            ConflictedFiles.Refresh();

            ApplicationModel.Current.Client.Notifications.Conflicts()
                .SampleResponsive(TimeSpan.FromSeconds(1))
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(notification => ConflictedFiles.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));
        }
    }
}
