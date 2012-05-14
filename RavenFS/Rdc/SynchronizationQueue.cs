namespace RavenFS.Rdc
{
	using System;
	using System.Collections.Concurrent;
	using System.Linq;
	using System.Threading;
	using Client;
	using Extensions;
	using Storage;

	public class SynchronizationQueue
	{
		private readonly TransactionalStorage storage;
		private const int DefaultLimitOfConcurrentSynchronizations = 5;

		private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> pendingSynchronizations =
			new ConcurrentDictionary<string, ConcurrentQueue<string>>();

		private readonly ConcurrentDictionary<string, ConcurrentBag<string>> activeSynchronizations =
			new ConcurrentDictionary<string, ConcurrentBag<string>>();

		private int numberOfActiveTasks;

		public SynchronizationQueue(TransactionalStorage storage)
		{
			this.storage = storage;
		}

		private int LimitOfConcurrentSynchronizations()
		{
			bool limit = false;
			int configuredLimit = 0;

			storage.Batch(
				accessor => limit = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenReplicationLimit, out configuredLimit));

			return limit ? configuredLimit : DefaultLimitOfConcurrentSynchronizations;
		}

		private int NumberOfActiveSynchronizationTasksFor(string destination)
		{
			numberOfActiveTasks = activeSynchronizations.GetOrAdd(destination, new ConcurrentBag<string>()).Count;
			return Thread.VolatileRead(ref numberOfActiveTasks);
		}

		public bool CanSynchronizeTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() > NumberOfActiveSynchronizationTasksFor(destination);
		}

		public int AvailableSynchronizationRequestsTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() - NumberOfActiveSynchronizationTasksFor(destination);
		}

		public void EnqueueSynchronization(string destination, string fileName)
		{
			var pendingForDestination = pendingSynchronizations.GetOrAdd(destination, new ConcurrentQueue<string>());

			if(pendingForDestination.Contains(fileName)) // if there is a file in pending synchronizations do not add it again
			{
				return;
			}

			pendingForDestination.Enqueue(fileName);
		}

		public bool TryDequeuePendingSynchronization(string destination, out string fileToSynchronize)
		{
			ConcurrentQueue<string> pendingForDestination;
			if (pendingSynchronizations.TryGetValue(destination, out pendingForDestination) == false)
			{
				throw new SynchronizationException(string.Format("No pending tasks found for {0}", destination));
			}

			return pendingForDestination.TryDequeue(out fileToSynchronize);
		}

		public void SynchronizationStarted(string fileName, string destination)
		{
			var activeForDestination = activeSynchronizations.GetOrAdd(destination, new ConcurrentBag<string>());

			activeForDestination.Add(fileName);
		}

		public void SynchronizationFinished(string fileName, string destination)
		{
			ConcurrentBag<string> activeDestinationTasks;

			activeSynchronizations.TryGetValue(destination, out activeDestinationTasks);
			string removingItem;
			
			if (activeDestinationTasks != null && activeDestinationTasks.TryTake(out removingItem))
			{
				return;
			}

			throw new SynchronizationException(string.Format("File {0} wasn't synchronized to server destination server {1}", fileName, destination));
		}
	}
}