namespace RavenFS.Util
{
	using System.Collections.Specialized;
	using Search;
	using Storage;

	public class SynchronizingFileStream : StorageStream
	{
		private SynchronizingFileStream(TransactionalStorage transactionalStorage, string fileName, StorageStreamAccess storageStreamAccess, NameValueCollection metadata, IndexStorage indexStorage) : base(transactionalStorage, fileName, storageStreamAccess, metadata, indexStorage)
		{
		}

		public bool PreventUploadComplete { get; set; }

		protected override void Dispose(bool disposing)
		{
			if (!PreventUploadComplete)
			{
				base.Dispose(disposing);
			}
		}

		public static SynchronizingFileStream CreatingOrOpeningAndWritting(TransactionalStorage storage, IndexStorage search, string fileName, NameValueCollection metadata)
		{
			return new SynchronizingFileStream(storage, fileName, StorageStreamAccess.CreateAndWrite, metadata, search)
			       	{ PreventUploadComplete = true };
		}
	}
}