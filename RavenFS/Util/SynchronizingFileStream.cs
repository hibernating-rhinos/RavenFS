namespace RavenFS.Util
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using Search;
	using Storage;

	public class SynchronizingFileStream : StorageStream
	{
		private SynchronizingFileStream(TransactionalStorage transactionalStorage, string fileName, StorageStreamAccess storageStreamAccess, NameValueCollection metadata, IndexStorage indexStorage) : base(transactionalStorage, fileName, storageStreamAccess, metadata, indexStorage)
		{
			LastWrittenPages = new List<PageInformation>();
		}

		public bool PreventDispose { get; set; }

		protected override void Dispose(bool disposing)
		{
			if (!PreventDispose)
			{
				base.Dispose(disposing);
			}
		}

		public static SynchronizingFileStream CreatingOrOpeningAndWritting(TransactionalStorage storage, IndexStorage search, string fileName, NameValueCollection metadata)
		{
			return new SynchronizingFileStream(storage, fileName, StorageStreamAccess.CreateAndWrite, metadata, search)
			       	{ PreventDispose = true };
		}

		public List<PageInformation> LastWrittenPages { get; set; }

		public override void Write(byte[] buffer, int offset, int count)
		{
			var innerOffset = 0;
			var innerBuffer = new byte[StorageConstants.MaxPageSize];
			while (innerOffset < count)
			{
				var toCopy = Math.Min(StorageConstants.MaxPageSize, count - innerOffset);
				if (toCopy == 0)
				{
					throw new Exception("Impossible");
				}

				Array.Copy(buffer, offset + innerOffset, innerBuffer, 0, toCopy);
				TransactionalStorage.Batch(
					accessor =>
					{
						var hashKey = accessor.InsertPage(innerBuffer, toCopy); // just insert - will associate later

						LastWrittenPages.Add(new PageInformation
						                     	{
						                     		Id = hashKey,
													Size = toCopy
						                     	});
					});
				innerOffset += toCopy;
			}
		}
	}
}