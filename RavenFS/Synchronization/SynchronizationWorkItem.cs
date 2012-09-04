namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.Linq;
	using System.Threading.Tasks;
	using Conflictuality;
	using Extensions;
	using NLog;
	using RavenFS.Client;
	using Storage;

	public abstract class SynchronizationWorkItem
	{
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		protected SynchronizationWorkItem(string fileName, TransactionalStorage storage)
		{
			Storage = storage;
			FileName = fileName;

			FileAndPages fileAndPages = null;
			Storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0,0));
			FileMetadata = fileAndPages.Metadata;

			this.conflictDetector = new ConflictDetector();
			this.conflictResolver = new ConflictResolver();
		}

		protected TransactionalStorage Storage { get; private set; }

		public string FileName { get; private set; }

		public Guid FileETag { get { return FileMetadata.Value<Guid>("ETag"); } }

		protected NameValueCollection FileMetadata { get; set; }

		protected Guid SourceServerId { get { return Storage.Id; } }

		public abstract SynchronizationType SynchronizationType { get; }

		public abstract Task<SynchronizationReport> Perform(string destination);

		protected void AssertLocalFileExistsAndIsNotConflicted(NameValueCollection sourceMetadata)
		{
			if (sourceMetadata == null)
			{
				throw new SynchronizationException(string.Format("File {0} does not exist", FileName));
			}

			if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenSynchronizationConflict))
			{
				throw new SynchronizationException(string.Format("File {0} is conflicted", FileName));
			}
		}

		protected ConflictItem CheckConflictWithDestination(NameValueCollection sourceMetadata, NameValueCollection destinationMetadata)
		{
			var conflict = conflictDetector.Check(FileName, destinationMetadata, sourceMetadata);
			var isConflictResolved = conflictResolver.IsResolved(destinationMetadata, conflict);

			// optimization - conflict checking on source side before any changes pushed
			if (conflict != null && !isConflictResolved)
			{
				return conflict;
			}

			return null;
		}

		protected async Task<SynchronizationReport> ApplyConflictOnDestination(ConflictItem conflict, string  destination, Logger log)
		{
			log.Debug("File '{0}' is in conflict with destination version from {1}. Applying conflict on destination", FileName, destination);

			var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);
			try
			{
				await destinationRavenFileSystemClient.Synchronization.ApplyConflictAsync(FileName, conflict.Current.Version, conflict.Remote.ServerId);
			}
			catch (Exception ex)
			{
				log.WarnException(string.Format("Failed to apply conflict on {0} for file '{1}'", destination, FileName), ex);
			}

			return new SynchronizationReport
			{
				FileName = FileName,
				Exception = new SynchronizationException(string.Format("File {0} is conflicted", FileName)),
				Type = SynchronizationType
			};
		}

		public void RefreshMetadata()
		{
			if (Storage != null)
			{
				FileAndPages fileAndPages = null;
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));
				FileMetadata = fileAndPages.Metadata;
			}
		}
	}
}