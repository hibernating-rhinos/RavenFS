using System.Collections.Concurrent;
using System.Collections.Generic;
using RavenFS.Notifications;

namespace RavenFS.Infrastructure.Connections
{
	public class ConnectionState
	{
		private readonly ConcurrentQueue<object> pendingMessages = new ConcurrentQueue<object>();

		private EventsTransport eventsTransport;


		public ConnectionState(EventsTransport eventsTransport)
		{
			this.eventsTransport = eventsTransport;
		}

		
		public void Send(Notification notification)
		{
            Enqueue(notification);
		}

		private void Enqueue(object msg)
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
			var items = new List<object>();
			object result;
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

		public void Disconnect()
		{
			if (eventsTransport != null)
				eventsTransport.Disconnect();
		}
	}
}