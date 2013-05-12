using System;
using System.Threading;
using System.Threading.Tasks;
using RavenFS.Client.Util;

namespace RavenFS.Client.Changes
{
	internal class LocalConnectionState
	{
		private readonly Action onZero;
		private readonly Task task;
		private int value = 1;
		private readonly ConcurrentSet<Task<IDisposable>> toDispose = new ConcurrentSet<Task<IDisposable>>();
		public Task Task
		{
			get { return task; }
		}

		public LocalConnectionState(Action onZero, Task task)
		{
			this.onZero = onZero;
			this.task = task;
		}

		public void Inc()
		{
			Interlocked.Increment(ref value);
		}

		public void Dec()
		{
			if (Interlocked.Decrement(ref value) == 0)
			{
				Dispose().Wait();
			}
		}

		public async Task Add(Task<IDisposable> disposableTask)
		{
			if (value == 0)
			{
				var disposable = await disposableTask;
				using (disposable) { }
				return;
			}

			toDispose.Add(disposableTask);
		}

		public async Task Dispose()
		{
			foreach (var disposableTask in toDispose)
			{
				var disposable = await disposableTask;
				using (disposable) { }
			}
			onZero();
		}

		public event Action<Notification> OnNotification = delegate { };

		public Action<Exception> OnError = delegate { };

		public void Send(Notification notification)
		{
            OnNotification(notification);
		}

		public void Error(Exception e)
		{
			OnError(e);	
		}
	}
}