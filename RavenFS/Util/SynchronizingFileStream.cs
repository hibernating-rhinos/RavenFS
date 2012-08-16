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

		public bool PreventUploadComplete { get; set; }

		protected override void Dispose(bool disposing)
		{
			if (innerBuffer != null && innerBufferOffset > 0)
			{
				WriteToStorage();
			}

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

		public List<PageInformation> LastWrittenPages { get; set; }

		public override void Write(byte[] buffer, int offset, int count)
		{
			var innerOffset = 0;

			while (innerOffset < count)
			{
				if (innerBuffer == null)
				{
					innerBuffer = new byte[StorageConstants.MaxPageSize];
				}

				var toCopy = Math.Min(StorageConstants.MaxPageSize - innerBufferOffset, count - innerOffset);
				if (toCopy == 0)
				{
					throw new Exception("Impossible");
				}

				Array.Copy(buffer, offset + innerOffset, innerBuffer, innerBufferOffset, toCopy);
				innerBufferOffset += toCopy;

				if (innerBufferOffset == StorageConstants.MaxPageSize)
				{
					WriteToStorage();
				}

				innerOffset += toCopy;
			}
		}

		protected override void WriteToStorage()
		{
			TransactionalStorage.Batch(
				accessor =>
				{
					var hashKey = accessor.InsertPage(innerBuffer, innerBufferOffset); // just insert - will associate later

					LastWrittenPages.Add(new PageInformation
					                     	{
					                     		Id = hashKey,
												Size = innerBufferOffset
					                     	});
				});

			innerBuffer = null;
			innerBufferOffset = 0;
		}
	}
}