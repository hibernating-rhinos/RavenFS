using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class RunSynchronizationCommand : Command
    {
        public override void Execute(object parameter)
        {
            ApplicationModel.Current.AddInfoNotification("Starting Synchronization");

            ApplicationModel.Current.Client.Synchronization.SynchronizeDestinationsAsync(true).Catch();
        }
    }
}
