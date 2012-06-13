namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Storage;

	public class SynchronizationQueue
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;
		private const int DefaultLimitOfConcurrentSynchronizations = 5;

		private readonly ConcurrentDictionary<string, ConcurrentQueue<SynchronizationWorkItem>> pendingSynchronizations =
			new ConcurrentDictionary<string, ConcurrentQueue<SynchronizationWorkItem>>();

		private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, string>> activeSynchronizations =
			new ConcurrentDictionary<string, ConcurrentDictionary<Guid, string>>();

		public SynchronizationQueue(TransactionalStorage storage)
		{
			this.storage = storage;
		}

		public IEnumerable<SynchronizationDetails> Pending
		{
			get
			{
				return from destinationPending in pendingSynchronizations
				       from pendingFile in destinationPending.Value
				       select new SynchronizationDetails
				              	{
				              		DestinationUrl = destinationPending.Key,
				              		FileName = pendingFile.FileName
				              	};
			}
		}

		public IEnumerable<SynchronizationDetails> Active
		{
			get
			{
				return from destinationActive in activeSynchronizations
				       from activeFile in destinationActive.Value
				       select new SynchronizationDetails
				       {
				              		DestinationUrl = destinationActive.Key,
				              		FileName = activeFile.Value
				              	};
			}
		}

		private int LimitOfConcurrentSynchronizations()
		{
			bool limit = false;
			int configuredLimit = 0;

			storage.Batch(
				accessor => limit = accessor.TryGetConfigurationValue(SynchronizationConstants.RavenSynchronizationLimit, out configuredLimit));

			return limit ? configuredLimit : DefaultLimitOfConcurrentSynchronizations;
		}

		private int NumberOfActiveSynchronizationTasksFor(string destination)
		{
			return activeSynchronizations.GetOrAdd(destination, new ConcurrentDictionary<Guid, string>()).Count;
		}

		public bool CanSynchronizeTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() > NumberOfActiveSynchronizationTasksFor(destination);
		}

		public int AvailableSynchronizationRequestsTo(string destination)
		{
			return LimitOfConcurrentSynchronizations() - NumberOfActiveSynchronizationTasksFor(destination);
		}

		public void EnqueueSynchronization(string destination, SynchronizationWorkItem workItem)
		{
			var pendingForDestination = pendingSynchronizations.GetOrAdd(destination, new ConcurrentQueue<SynchronizationWorkItem>());

			if(pendingForDestination.Contains(workItem)) // if there is a file in pending synchronizations do not add it again
			{
				log.Debug("{0} for a file {1} and a destination {2} was already existed in a pending queue", workItem.GetType().Name, workItem.FileName, destination);
				return;
			}

			pendingForDestination.Enqueue(workItem);
			log.Debug("{0} for a file {1} and a destination {2} was enqueued", workItem.GetType().Name, workItem.FileName, destination);
		}

		public bool TryDequeuePendingSynchronization(string destination, out SynchronizationWorkItem workItem)
		{
			ConcurrentQueue<SynchronizationWorkItem> pendingForDestination;
			if (pendingSynchronizations.TryGetValue(destination, out pendingForDestination) == false)
			{
				log.Warn("Could not get a pending synchronization queue for {0}", destination);
				workItem = null;
				return false;
			}

			return pendingForDestination.TryDequeue(out workItem);
		}

		public void SynchronizationStarted(string fileName, Guid etag, string destination)
		{
			var activeForDestination = activeSynchronizations.GetOrAdd(destination, new ConcurrentDictionary<Guid, string>());

			if(activeForDestination.TryAdd(etag, fileName))
			{
				log.Debug("File '{0}' with ETag {1} was added to an active synchronization queue for a destination {2}", fileName,
				          etag, destination);
			}
		}

		public void SynchronizationFinished(string fileName, Guid etag, string destination)
		{
			ConcurrentDictionary<Guid, string> activeDestinationTasks;

			if (activeSynchronizations.TryGetValue(destination, out activeDestinationTasks) == false)
			{
				log.Warn("Could not get an active synchronization queue for {0}", destination);
				return;
			}
			
			string removingItem;
			if(activeDestinationTasks.TryRemove(etag, out removingItem))
			{
				log.Debug("File '{0}' with ETag {1} was removed from an active synchronization queue for a destination {2}", fileName,
						  etag, destination);
			}
		}
	}
}