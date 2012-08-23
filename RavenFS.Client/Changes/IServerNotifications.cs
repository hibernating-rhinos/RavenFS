using System;
using System.Threading.Tasks;

namespace RavenFS.Client.Changes
{
    public interface IServerNotifications
	{
		Task ConnectionTask { get; }
        Task WhenSubscriptionsActive();

	    IObservable<ConfigChange> ConfigurationChanges();
	    IObservable<ConflictDetected> ConflictDetected();
	    IObservable<FileChange> FolderChanges(string folder);
        IObservable<SynchronizationUpdate> SynchronizationUpdates();
	}
}