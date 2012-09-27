using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RavenFS.Client.Connections;
using RavenFS.Client.Util;

namespace RavenFS.Client.Changes
{
	public class ServerNotifications : IServerNotifications, IObserver<string>, IDisposable
	{
		private readonly string url;
        private readonly AtomicDictionary<NotificationSubject> subjects = new AtomicDictionary<NotificationSubject>(StringComparer.InvariantCultureIgnoreCase);
        private readonly ConcurrentSet<Task> pendingConnectionTasks = new ConcurrentSet<Task>();
		private int reconnectAttemptsRemaining;
		private IDisposable connection;

		private static int connectionCounter;
		private readonly string id;
	    private Task connectionTask;
	    private object gate = new object();

		public ServerNotifications(string url)
		{
			id = Interlocked.Increment(ref connectionCounter) + "/" +
				 Base62Util.Base62Random();
			this.url = url;
		}

		private Task EstablishConnection()
		{
			var request= (HttpWebRequest) WebRequest.Create(url + "/changes/events?id=" + id);
		    request.Method = "GET";

			return request
				.ServerPullAsync()
				.ContinueWith(task =>
								{
									if (task.IsFaulted && reconnectAttemptsRemaining > 0)
									{
										reconnectAttemptsRemaining--;
										return EstablishConnection();
									}
									reconnectAttemptsRemaining = 3; // after the first successful try, we will retry 3 times before giving up
									connection = (IDisposable)task.Result;
									task.Result.Subscribe(this);
									return task;
								})
				.Unwrap();
		}

	    public Task ConnectionTask
	    {
	        get
	        {
	            EnsureConnectionInitiated();
	            return connectionTask;
	        }
	    }

	    private void EnsureConnectionInitiated()
        {
            if (connectionTask != null)
            {
                return;
            }

            lock(gate)
            {
                if (connectionTask != null)
                {
                    return;
                }

                connectionTask = EstablishConnection()
                .ObserveException();
            }
        }

		private Task AfterConnection(Func<Task> action)
		{
			return ConnectionTask.ContinueWith(task =>
			{
				task.AssertNotFailed();
				return action();
			})
			.Unwrap();
		}

	    public Task WhenSubscriptionsActive()
	    {
	        return TaskEx.WhenAll(pendingConnectionTasks);
	    }

        public IObservable<ConfigChange> ConfigurationChanges()
        {
            EnsureConnectionInitiated();

            var observable = subjects.GetOrAdd("config", s => new NotificationSubject<ConfigChange>(
                                                               () => ConfigureConnection("watch-config"), 
                                                               () => ConfigureConnection("unwatch-config"), 
                                                               item => true));

            return (IObservable<ConfigChange>)observable;
        }

	    private void ConfigureConnection(string command, string value = "")
	    {
	        var task = AfterConnection(() => Send(command, value));

            pendingConnectionTasks.Add(task);
	        task.ContinueWith(_ => pendingConnectionTasks.TryRemove(task));
	    }

	    public IObservable<ConflictDetected> ConflictDetected()
        {
            EnsureConnectionInitiated();

            var observable = subjects.GetOrAdd("conflicts", s => new NotificationSubject<ConflictDetected>(
                                                               () => ConfigureConnection("watch-conflicts"), 
                                                               () => ConfigureConnection("unwatch-conflicts"), 
                                                               item => true));

            return (IObservable<ConflictDetected>)observable;
        }

        public IObservable<FileChange> FolderChanges(string folder)
        {
            if (!folder.StartsWith("/"))
            {
                throw new ArgumentException("folder must start with /");
            }

            var canonicalisedFolder = folder.TrimStart('/');

            EnsureConnectionInitiated();

            var observable = subjects.GetOrAdd("folder/" + canonicalisedFolder, s => new NotificationSubject<FileChange>(
                                                               () => ConfigureConnection("watch-folder", canonicalisedFolder), 
                                                               () => ConfigureConnection("unwatch-folder", canonicalisedFolder), 
                                                               f => f.File.StartsWith(canonicalisedFolder, StringComparison.InvariantCultureIgnoreCase)));

            return (IObservable<FileChange>)observable;
        }

        public IObservable<SynchronizationUpdate> SynchronizationUpdates()
        {
            EnsureConnectionInitiated();

            var observable = subjects.GetOrAdd("sync", s => new NotificationSubject<SynchronizationUpdate>(
                                                               () => ConfigureConnection("watch-sync"),
                                                               () => ConfigureConnection("unwatch-sync"),
                                                               x => true));

            return (IObservable<SynchronizationUpdate>)observable;
        }

		internal IObservable<UploadCancelled> CancelledUploads()
		{
			EnsureConnectionInitiated();

			var observable = subjects.GetOrAdd("cancellations", s => new NotificationSubject<UploadCancelled>(
															   () => ConfigureConnection("watch-cancellations"),
															   () => ConfigureConnection("unwatch-cancellations"),
															   x => true));

			return (IObservable<UploadCancelled>)observable;
		}

		private Task Send(string command, string value)
		{
			try
			{
				var sendUrl = url + "/changes/config?id=" + id + "&command=" + command;
				if (string.IsNullOrEmpty(value) == false)
					sendUrl += "&value=" + Uri.EscapeUriString(value);

                var request = (HttpWebRequest)WebRequest.Create(sendUrl);
                request.Method = "GET";
				return request.GetResponseAsync().ObserveException();
			}
			catch (Exception e)
			{
				return Util.TaskExtensions.FromException(e).ObserveException();
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;

			DisposeAsync();
		}

		private bool disposed;
		public Task DisposeAsync()
		{
			if (disposed)
				return TaskEx.FromResult(true);
			disposed = true;
			reconnectAttemptsRemaining = 0;

			if (connection == null)
			{
                return TaskEx.FromResult(true);
			}

            foreach (var subject in subjects)
            {
                subject.Value.OnCompleted();
            }

			return Send("disconnect", null).
				ContinueWith(_ =>
								{
									try
									{
										connection.Dispose();
									}
									catch (Exception)
									{
									}
								});
		}

        public void OnNext(string dataFromConnection)
        {
            var notification = NotificationJSonUtilities.Parse<Notification>(dataFromConnection);

            if (notification is Heartbeat)
            {
                return;
            }

            foreach (var subject in subjects)
            {
                subject.Value.OnNext(notification);
            }

        }

	    public void OnError(Exception error)
		{
			if (reconnectAttemptsRemaining <= 0)
				return;

			EstablishConnection()
				.ObserveException()
				.ContinueWith(task =>
								{
									if (task.IsFaulted == false)
										return;

                                    foreach (var subject in subjects)
                                    {
                                        subject.Value.OnError(task.Exception);
                                    }
                                    subjects.Clear();
								});
		}

		public void OnCompleted()
		{
		}
	}
}