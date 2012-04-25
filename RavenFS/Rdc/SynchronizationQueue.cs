namespace RavenFS.Rdc
{
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Threading;

	public class SynchronizationQueue
	{
		public class IntHolder
		{
			public int Value;
		}

		private readonly object queueLock = new object();

		ConcurrentDictionary<string, ConcurrentQueue<string>> pendingSynchronizations = new ConcurrentDictionary<string, ConcurrentQueue<string>>();

		private readonly ConcurrentDictionary<string, IntHolder> activeSynchronizationTasks = new ConcurrentDictionary<string, IntHolder>();

		public int NumberOfActiveSynchronizationTasksFor(string destination)
		{
			var holder = activeSynchronizationTasks.GetOrAdd(destination, new IntHolder());
			
			return Thread.VolatileRead(ref holder.Value);
		}

		public string GetFirstFileNameToSynchronizeFor(string destination)
		{
			string fileName;

			lock (queueLock)
			{
				var queue = pendingSynchronizations.GetOrAdd(destination, new ConcurrentQueue<string>());

				if(queue.TryDequeue(out fileName))
				{

				}
			}
			return fileName;
		}
	}
}