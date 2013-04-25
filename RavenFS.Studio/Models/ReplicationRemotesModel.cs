using System;
using System.Reactive.Linq;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;

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
                .ConfigurationChanges()
                .TakeUntil(Unloaded)
                .ObserveOnDispatcher()
                .Subscribe(change => HandleConfigChange(change));
        }

        private void HandleConfigChange(ConfigChange change)
        {
	        if (change.Action != ConfigChangeAction.Set)
		        return;

	        switch (change.Name)
	        {
		        case SynchronizationConstants.RavenSynchronizationDestinations:
			        BeginDestinationServersUpdate();
			        break;
		        case SynchronizationConstants.RavenSynchronizationSourcesBasePath:
			        BeginSourceServersUpdate();
			        break;
	        }
        }

	    private void BeginDestinationServersUpdate()
        {
            ApplicationModel.Current.Client.Config.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations)
                .ContinueOnUIThread(t =>
                {
	                if (t.IsFaulted)
		                ApplicationModel.Current.AddErrorNotification(t.Exception, "Could not load Replication configuration");

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
		                                        ApplicationModel.Current.AddErrorNotification(t.Exception,
		                                                                                      "Could not load Replication configuration");

	                                        if (t.Result != null)
                                            {
                                                var sources = t.Result.GetValues("url");
                                                SourceServers.Match(sources);
                                            }
                                        });
        }
    }
}
