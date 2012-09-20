namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Linq;
	using Infrastructure;
	using Rdc.Wrapper;
	using Storage;
	using Util;

	public class SynchronizationStrategy
	{
		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;

		public SynchronizationStrategy(TransactionalStorage storage, SigGenerator sigGenerator)
		{
			this.storage = storage;
			this.sigGenerator = sigGenerator;
		}

		public bool Filter(FileHeader file, Guid destinationId, IEnumerable<FileHeader> candidatesToSynchronization)
		{
			// prevent synchronization back to source
			if(file.Metadata[SynchronizationConstants.RavenSynchronizationSource] == destinationId.ToString())
			{
				return false;
			}

			if (file.Name.EndsWith(SynchronizationNamesHelper.DownloadingFilePrefix))
			{
				return false;
			}

			if (FileIsBeingUploaded(file))
			{
				return false;
			}

			if(ExistsReplicationTombstone(file.Name, candidatesToSynchronization))
			{
				return false;
			}

			return true;
		}

		private static bool ExistsReplicationTombstone(string name, IEnumerable<FileHeader> candidatesToSynchronization)
		{
			return
				candidatesToSynchronization.Any(
					x =>
					x.Metadata[SynchronizationConstants.RavenDeleteMarker] != null &&
					x.Metadata[SynchronizationConstants.RavenRenameFile] == name);
		}

		private static bool FileIsBeingUploaded(FileHeader header)
		{
			return header.TotalSize == null || header.TotalSize != header.UploadedSize ||
				   (header.Metadata[SynchronizationConstants.RavenDeleteMarker] == null && header.Metadata["Content-MD5"] == null);
		}

		public SynchronizationWorkItem DetermineWork(string file, NameValueCollection localMetadata, NameValueCollection destinationMetadata, out NoSyncReason reason)
		{
			reason = NoSyncReason.Unknown;

			if (localMetadata == null)
			{
				reason = NoSyncReason.SourceFileNotExist;
				return null;
			}

			if (destinationMetadata != null &&
					destinationMetadata[SynchronizationConstants.RavenSynchronizationConflict] != null
					&& destinationMetadata[SynchronizationConstants.RavenSynchronizationConflictResolution] == null)
			{
				reason = NoSyncReason.DestinationFileConflicted;
				return null;
			}

			if (localMetadata[SynchronizationConstants.RavenSynchronizationConflict] != null)
			{
				reason = NoSyncReason.SourceFileConflicted;
				return null;
			}

			if (localMetadata[SynchronizationConstants.RavenDeleteMarker] != null)
			{
				var rename = localMetadata[SynchronizationConstants.RavenRenameFile];

				if (rename != null)
				{
					return new RenameWorkItem(file, rename, storage);
				}
				return new DeleteWorkItem(file, storage);
			}

			if (destinationMetadata != null && Historian.IsDirectChildOfCurrent(localMetadata, destinationMetadata))
			{
				reason = NoSyncReason.ContainedInDestinationHistory;
				return null;
			}

			if (destinationMetadata != null && localMetadata["Content-MD5"] == destinationMetadata["Content-MD5"]) // file exists on dest and has the same content
			{
				// check metadata to detect if any synchronization is needed
				if (localMetadata.AllKeys.Except(new[] { "ETag", "Last-Modified" }).Any(key => !destinationMetadata.AllKeys.Contains(key) || localMetadata[key] != destinationMetadata[key]))
				{
					return new MetadataUpdateWorkItem(file, destinationMetadata, storage);
				}

				reason = NoSyncReason.SameContentAndMetadata;

				return null; // the same content and metadata - no need to synchronize
			}
			return new ContentUpdateWorkItem(file, storage, sigGenerator);
		}
	}
}