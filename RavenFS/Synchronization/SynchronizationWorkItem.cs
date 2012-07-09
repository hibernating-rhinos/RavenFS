namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.Linq;
	using System.Threading.Tasks;
	using Conflictuality;
	using NLog;
	using RavenFS.Client;

	public abstract class SynchronizationWorkItem
	{
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		protected SynchronizationWorkItem(string fileName, Guid sourceServerId)
		{
			FileName = fileName;
			SourceServerId = sourceServerId;

			this.conflictDetector = new ConflictDetector();
			this.conflictResolver = new ConflictResolver();
		}

		public string FileName { get; private set; }
		public Guid SourceServerId { get; private set; }

		public abstract SynchronizationType SynchronizationType { get; }

		public abstract Task<SynchronizationReport> Perform(string destination);

		protected void AssertLocalFileExistsAndIsNotConflicted(NameValueCollection sourceMetadata)
		{
			if (sourceMetadata == null)
			{
				throw new SynchronizationException(string.Format("File {0} does not exists", FileName));
			}

			if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenSynchronizationConflict))
			{
				throw new SynchronizationException(string.Format("File {0} is conflicted", FileName));
			}
		}

		protected ConflictItem CheckConflictWithDestination(NameValueCollection sourceMetadata, NameValueCollection destinationMetadata)
		{
			var conflict = conflictDetector.Check(destinationMetadata, sourceMetadata);
			var isConflictResolved = conflictResolver.IsResolved(destinationMetadata, conflict);

			// optimization - conflict checking on source side before any changes pushed
			if (conflict != null && !isConflictResolved)
			{
				return conflict;
			}

			return null;
		}

		protected Task<SynchronizationReport> ApplyConflictOnDestination(ConflictItem conflict, string  destination, Logger log)
		{
			log.Debug("File '{0}' is in conflict with destination version from {1}. Applying conflict on destination", FileName, destination);

			var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

			return destinationRavenFileSystemClient.Synchronization
			.ApplyConflictAsync(FileName, conflict.Current.Version, conflict.Remote.ServerId)
			.ContinueWith(task =>
			{
				if (task.Exception != null)
				{
					log.WarnException(
						string.Format("Failed to apply conflict on {0} for file '{1}'", destination, FileName),
						task.Exception.ExtractSingleInnerException());
				}

				return new SynchronizationReport
						{
							FileName = FileName,
							Exception = new SynchronizationException(string.Format("File {0} is conflicted", FileName)),
							Type = SynchronizationType
						};
			});
		}
	}
}