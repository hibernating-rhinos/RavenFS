using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RavenFS.Notifications;
using RavenFS.Util;

namespace RavenFS.Infrastructure.Connections
{
	public class ConnectionState
	{
		private readonly ConcurrentQueue<Notification> pendingMessages = new ConcurrentQueue<Notification>();

		private EventsTransport eventsTransport;


	    private int watchConfig;
	    private int watchConflicts;
	    private int watchSync;
        private ConcurrentSet<string> matchingFolders = new ConcurrentSet<string>(StringComparer.InvariantCultureIgnoreCase);

		public ConnectionState(EventsTransport eventsTransport)
		{
			this.eventsTransport = eventsTransport;
		}

		
		public void Send(Notification notification)
		{
            if (ShouldSend(notification))
            {
                Enqueue(notification);
            }
		}

	    private bool ShouldSend(Notification notification)
	    {
	        if (notification is FileChange && matchingFolders.Any(f => ((FileChange)notification).File.StartsWith(f, StringComparison.InvariantCultureIgnoreCase)))
	        {
	            return true;
	        }
            
            if (notification is ConfigChange && watchConfig > 0)
            {
                return true;
            }

            if (notification is ConflictDetected && watchConflicts > 0)
            {
                return true;
            }

            if (notification is SynchronizationUpdate && watchSync > 0)
            {
                return true;
            }

	        return false;
	    }

	    private void Enqueue(Notification msg)
		{
			if (eventsTransport == null || eventsTransport.Connected == false)
			{
				pendingMessages.Enqueue(msg);
				return;
			}

			eventsTransport.SendAsync(msg)
				.ContinueWith(task =>
								{
									if (task.IsFaulted == false)
										return;
									pendingMessages.Enqueue(msg);
								});
		}

		public void Reconnect(EventsTransport transport)
		{
			eventsTransport = transport;
			var items = new List<Notification>();
			Notification result;
			while (pendingMessages.TryDequeue(out result))
			{
				items.Add(result);
			}

			eventsTransport.SendManyAsync(items)
				.ContinueWith(task =>
								{
									if (task.IsFaulted == false)
										return;
									foreach (var item in items)
									{
										pendingMessages.Enqueue(item);
									}
								});
		}

        public void WatchConflicts()
        {
            Interlocked.Increment(ref watchConflicts);
        }

        public void UnwatchConflicts()
        {
            Interlocked.Decrement(ref watchConflicts);
        }

        public void WatchConfig()
        {
            Interlocked.Increment(ref watchConfig);
        }

        public void UnwatchConfig()
        {
            Interlocked.Decrement(ref watchConfig);
        }

        public void WatchSync()
        {
            Interlocked.Increment(ref watchSync);
        }

        public void UnwatchSync()
        {
            Interlocked.Decrement(ref watchSync);
        }

        public void WatchFolder(string folder)
        {
            matchingFolders.TryAdd(folder);
        }

        public void UnwatchFolder(string folder)
        {
            matchingFolders.TryRemove(folder);
        }

		public void Disconnect()
		{
			if (eventsTransport != null)
				eventsTransport.Disconnect();
		}
	}
}