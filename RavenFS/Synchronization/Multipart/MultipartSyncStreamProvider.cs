namespace RavenFS.Synchronization.Multipart
{
	using System.Net.Http;
	using Client;
	using Storage;
	using Util;

	public abstract class MultipartSyncStreamProvider : MultipartStreamProvider
	{
		protected readonly SynchronizingFileStream synchronizingFile;
		protected readonly StorageStream localFile;
		protected readonly TransactionalStorage storage;

		protected MultipartSyncStreamProvider(SynchronizingFileStream synchronizingFile, StorageStream localFile, TransactionalStorage storage)
		{
			this.synchronizingFile = synchronizingFile;
			this.localFile = localFile;
			this.storage = storage;

			BytesTransfered = BytesCopied = NumberOfFileParts = 0;
		}

		public long BytesTransfered { get; protected set; }
		public long BytesCopied { get; protected set; }
		public long NumberOfFileParts { get; protected set; }

		public SynchronizationReport SynchronizationReport { get; protected set; }
	}
}
