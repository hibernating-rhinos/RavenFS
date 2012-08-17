using System;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using RavenFS.Client.Connections;
using RavenFS.Client.Util;

namespace RavenFS.Client.Changes
{
	public class ServerNotifications : IServerNotifications, IObserver<string>, IDisposable
	{
		private readonly string url;
		private readonly AtomicDictionary<LocalConnectionState> counters = new AtomicDictionary<LocalConnectionState>(StringComparer.InvariantCultureIgnoreCase);
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

		public IObservableWithTask<Notification> All()
		{
            EnsureConnectionInitiated();

			var counter = counters.GetOrAdd("all", s =>
			{
				var subscriptionTask = AfterConnection(() => TaskEx.FromResult(true));

				return new LocalConnectionState(
					() =>
					{
						counters.Remove("all");
					},
					subscriptionTask);
			});
			counter.Inc();
			var taskedObservable = new TaskedObservable<Notification>(
				counter,
				notification => !(notification is Heartbeat));

			counter.OnNotification += taskedObservable.Send;
			counter.OnError = taskedObservable.Error;

			var disposableTask = counter.Task.ContinueWith(task =>
			{
				if (task.IsFaulted)
					return null;
				return (IDisposable)new DisposableAction(() =>
															{
																try
																{
																	connection.Dispose();
																}
																catch (Exception)
																{
																	// nothing to do here
																}
															});
			});

			counter.Add(disposableTask);
			return taskedObservable;

		}

        public IObservable<ConfigChange> ConfigurationChanges()
        {
            return All().OfType<ConfigChange>();
        }

        public IObservable<ConflictDetected> ConflictDetected()
        {
            return All().OfType<ConflictDetected>();
        }

        public IObservable<FileChange> FolderChanges(string folder)
        {
            if (!folder.StartsWith("/"))
            {
                throw new ArgumentException("folder must start with /");
            }

            var canonicalisedFolder = folder.TrimStart('/');

            return All()
                .OfType<FileChange>()
                .Where(f => f.File.StartsWith(canonicalisedFolder, StringComparison.InvariantCultureIgnoreCase));
        }

        public IObservable<SynchronizationUpdate> SynchronizationUpdates(SynchronizationDirection synchronizationDirection)
        {
            return All().OfType<SynchronizationUpdate>().Where(x => x.SynchronizationDirection == synchronizationDirection);
        }

		private Task Send(string command, string value)
		{
			try
			{
				var sendUrl = url + "/changes/config?id=" + id + "&command=" + command;
				if (string.IsNullOrEmpty(value) == false)
					sendUrl += "&value=" + Uri.EscapeUriString(value);

                var request = (HttpWebRequest)WebRequest.Create(url + "/changes/events?id=" + id);
                request.Method = "GET";
				return request.GetResponseAsync().ObserveException();
			}
			catch (Exception e)
			{
				return Util.TaskExtensions.FromException<bool>(e).ObserveException();
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
			foreach (var keyValuePair in counters)
			{
				keyValuePair.Value.Dispose();
			}
			if (connection == null)
			{
                return TaskEx.FromResult(true);
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

            foreach (var counter in counters)
            {
                counter.Value.Send(notification);
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

									foreach (var keyValuePair in counters)
									{
										keyValuePair.Value.Error(task.Exception);
									}
									counters.Clear();
								});
		}

		public void OnCompleted()
		{
		}
	}
}