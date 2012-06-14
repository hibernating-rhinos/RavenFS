namespace RavenFS.Synchronization
{
	using System.Collections.Specialized;
	using System.Linq;
	using System.Threading.Tasks;
	using Conflictuality;
	using NLog;
	using RavenFS.Client;

	public abstract class SynchronizationWorkItem
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		protected SynchronizationWorkItem(string fileName, string sourceServerUrl)
		{
			FileName = fileName;
			SourceServerUrl = sourceServerUrl;

			this.conflictDetector = new ConflictDetector();
			this.conflictResolver = new ConflictResolver();
		}

		public string FileName { get; private set; }
		public string SourceServerUrl { get; private set; }

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

		protected ConflictItem GetConflictWithDestination(NameValueCollection sourceMetadata, NameValueCollection destinationMetadata)
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
	}
}