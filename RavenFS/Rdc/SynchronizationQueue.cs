namespace RavenFS.Rdc
{
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using Client;

	public class SynchronizationQueue
	{
		private readonly ConcurrentDictionary<string, ConcurrentBag<string>> pendingSynchronizations =
			new ConcurrentDictionary<string, ConcurrentBag<string>>();

		private readonly ConcurrentDictionary<string, ConcurrentBag<string>> activeSynchronizations =
			new ConcurrentDictionary<string, ConcurrentBag<string>>();

		public int NumberOfActiveSynchronizationTasksFor(string destination)
		{
			return activeSynchronizations.GetOrAdd(destination, new ConcurrentBag<string>()).Count;
		}

		public void AddPending(string destination, string fileName)
		{
			var pendingForDestination = pendingSynchronizations.GetOrAdd(destination, new ConcurrentBag<string>());

			if(pendingForDestination.Contains(fileName)) // if there is a file in pending collection do not add it again
			{
				return;
			}

			pendingForDestination.Add(fileName);
		}

		public IEnumerable<string> GetPendingFiles(string destination, int take)
		{
			ConcurrentBag<string> pendingForDestination;
			if(pendingSynchronizations.TryGetValue(destination, out pendingForDestination) == false)
			{
				throw new SynchronizationException(string.Format("No pending tasks found for {0}", destination));
			}

			return pendingForDestination.Take(take);
		}

		public void SynchronizationStarted(string fileName, string destination)
		{
			ConcurrentBag<string> pendingForDestination;
			if (pendingSynchronizations.TryGetValue(destination, out pendingForDestination) == false)
			{
				throw new SynchronizationException(string.Format("No pending tasks found for {0}", destination));
			}

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

			throw new SynchronizationException(string.Format("File {0} isn't synchronized currently to {1} server", fileName, destination));
		}
	}
}