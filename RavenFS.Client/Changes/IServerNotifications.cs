using System;
using System.Threading.Tasks;

namespace RavenFS.Client.Changes
{
    public interface IServerNotifications
	{
		Task ConnectionTask { get; }
	    IObservableWithTask<Notification> All();

	    IObservable<ConfigChange> ConfigurationChanges();
	    IObservable<ConflictDetected> ConflictDetected();
	    IObservable<FileChange> FolderChanges(string folder);
        IObservable<SynchronizationUpdate> SynchronizationUpdates(SynchronizationDirection synchronizationDirection);
	}
}