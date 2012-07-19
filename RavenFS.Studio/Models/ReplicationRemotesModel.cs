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
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Reactive.Linq;

namespace RavenFS.Studio.Models
{
    public class ReplicationRemotesModel : ViewModel
    {
        private ICommand addDestinationCommand;
        public BindableCollection<string> DestinationServers { get; private set; }

        public BindableCollection<string> SourceServers { get; private set; } 

        public ReplicationRemotesModel()
        {
            DestinationServers = new BindableCollection<string>(x => x);
            SourceServers = new BindableCollection<string>(x => x);
        }

        public ICommand AddDestinationCommand
        {
            get { return addDestinationCommand ?? (addDestinationCommand = new AddSynchronizationDestinationCommand()); }
        }

        protected override void OnViewLoaded()
        {
            BeginSourceServersUpdate();
            BeginDestinationServersUpdate();

            ApplicationModel.Current.Client.Notifications
                .ConfigChanges()
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(change => HandleConfigChange(change));
        }

        private void HandleConfigChange(ConfigChange change)
        {
            if (change.Action != ConfigChangeAction.Set)
            {
                return;
            }

            if (change.Name == SynchronizationConstants.RavenSynchronizationDestinations)
            {
                BeginDestinationServersUpdate();
            }
            else if (change.Name == SynchronizationConstants.RavenSynchronizationSourcesBasePath)
            {
                BeginSourceServersUpdate();
            }
        }

        private void BeginDestinationServersUpdate()
        {
            ApplicationModel.Current.Client.Config.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations)
                .ContinueOnUIThread(t =>
                {
                    if (t.IsFaulted)
                    {
                        ApplicationModel.Current.AddErrorNotification(t.Exception, "Could not load Replication configuration");
                    }

                    if (t.Result != null)
                    {
                        var destinations = t.Result.GetValues("url");
                        DestinationServers.Match(destinations);
                    }
                });
        }

        private void BeginSourceServersUpdate()
        {
            ApplicationModel.Current.Client.Config.GetConfig(SynchronizationConstants.RavenSynchronizationSourcesBasePath)
                .ContinueOnUIThread(t =>
                                        {
                                            if (t.IsFaulted)
                                            {
                                                ApplicationModel.Current.AddErrorNotification(t.Exception, "Could not load Replication configuration");
                                            }

                                            if (t.Result != null)
                                            {
                                                var sources = t.Result.GetValues("url");
                                                SourceServers.Match(sources);
                                            }
                                        });
        }
    }
}
