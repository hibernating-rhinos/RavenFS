using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SignalR.Client;

namespace RavenFS.Client
{
    public class NotificationsManager
    {
        private RavenFileSystemClient client;
        private object lockObject = new object();
        private Connection notificationClient;
        private int currentObservers;

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
            return GetConnection().Start();
        }

        public IObservable<FileChange> FolderChanges(string folder)
        {
            if (!folder.StartsWith("/"))
            {
                throw new ArgumentException("folder must start with /");
            }

            var canonicalisedFolder = folder.TrimStart('/');

            var observable = Observable.Create<FileChange>(
                (inner) =>
                    {
                        var connection = GetConnection();
                        StartListening();

                        Action<string> messageHandler =
                            message =>
                                {
                                    var fileChange = JsonConvert.DeserializeObject<FileChange>(message);
                                    if (fileChange.File.StartsWith(canonicalisedFolder, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        inner.OnNext(fileChange);
                                    }
                                };

                        connection.Received += messageHandler;

                        return () =>
                                   {
                                       connection.Received -= messageHandler;
                                       StopListening();
                                   };
                    });
           

            return observable;
        }

        private void StopListening()
        {
            lock (lockObject)
            {
                currentObservers--;

                if (currentObservers == 0)
                {
                    GetConnection().Stop();
                }
            }
        }

        private void StartListening()
        {
            lock (lockObject)
            {
                if (currentObservers == 0)
                {
                    GetConnection().Start();
                }

                currentObservers++;
            }
        }

        private Connection GetConnection()
        {
            lock (lockObject)
            {
                return notificationClient ?? (notificationClient = new Connection(client.ServerUrl + "/notifications"));
            }
            
        }
    }
}