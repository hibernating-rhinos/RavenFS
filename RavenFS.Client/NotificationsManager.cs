using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SignalR.Client;

namespace RavenFS.Client
{
    public class NotificationsManager
    {
        private readonly RavenFileSystemClient client;
        private readonly object lockObject = new object();
        private Connection notificationClient;
        private int currentObservers;
        private readonly Subject<Notification> rawNotifications = new Subject<Notification>();
        
        public NotificationsManager(RavenFileSystemClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Ensures that the client is listening for messages from the server.
        /// Note that calling FolderChanges is sufficient to make the client connect. This method
        /// is only needed on occasions (like testing) when you need to be sure the client is connected before continuing
        /// </summary>
        /// <returns>A Task that completes once the connection is established.</returns>
        public Task Connect()
        {
            return GetOrCreateConnection().Start();
        }

        public IObservable<ConfigChange> ConfigChanges()
        {
            return Notifications().OfType<ConfigChange>();
        } 

		public IObservable<ConflictDetected> ConflictDetections()
		{
			return Notifications().OfType<ConflictDetected>();
		}

        public IObservable<FileChange> FolderChanges(string folder)
        {
            if (!folder.StartsWith("/"))
            {
                throw new ArgumentException("folder must start with /");
            }

            var canonicalisedFolder = folder.TrimStart('/');

            return Notifications()
                .OfType<FileChange>()
                .Where(f => f.File.StartsWith(canonicalisedFolder, StringComparison.InvariantCultureIgnoreCase));
        }

    	public IObservable<SynchronizationUpdate> SynchronizationUpdates(SynchronizationDirection synchronizationDirection)
    	{
    		return Notifications().OfType<SynchronizationUpdate>().Where(x => x.SynchronizationDirection == synchronizationDirection);
    	}

        public IObservable<Notification> Notifications()
        {
            StartListening();

            return Observable.Create<Notification>((observer) => new CompositeDisposable(
                                                                rawNotifications.Subscribe(observer),
                                                                Disposable.Create(StopListening)));
        }

        private void StopListening()
        {
            lock (lockObject)
            {
                currentObservers--;

                if (currentObservers == 0 && notificationClient != null)
                {
                    // Connection.Stop seems to hang if called on the UI thread
                    var capturedClient = notificationClient;
                    Task.Factory.StartNew(capturedClient.Stop);

                    notificationClient = null;
                }
            }
        }

        private void StartListening()
        {
            lock (lockObject)
            {
                if (currentObservers == 0)
                {
                    GetOrCreateConnection().Start();
                }

                currentObservers++;
            }
        }

        private Connection GetOrCreateConnection()
        {
            lock (lockObject)
            {
                if (notificationClient == null)
                {
                    notificationClient = new Connection(client.ServerUrl + "/notifications");
                    notificationClient.Received += message =>
                                                       {
                                                           var notification = NotificationJSonUtilities.Parse<Notification>(message);
                                                           rawNotifications.OnNext(notification);
                                                       };
                }

                return notificationClient;
            }
        }
    }
}