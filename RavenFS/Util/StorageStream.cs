using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using RavenFS.Search;
using RavenFS.Storage;

namespace RavenFS.Util
{
	using Infrastructure;

	public class StorageStream : Stream
    {
        public TransactionalStorage TransactionalStorage { get; private set; }
        public StorageStreamAccess StorageStreamAccess { get; private set; }

        private FileHeader fileHeader;
        public string Name { get; private set; }

        public NameValueCollection Metadata { get; private set; }

        private const int PagesBatchSize = 64;
        private FileAndPages fileAndPages;
        private long currentOffset;
        private long currentPageFrameSize { get { return fileAndPages.Pages.Sum(item => item.Size); } }
        private long currentPageFrameOffset;
        private bool disposed = false;

		protected byte[] innerBuffer;
		protected int innerBufferOffset = 0;

        public static StorageStream Reading(TransactionalStorage transactionalStorage, string fileName)
        {
            return new StorageStream(transactionalStorage, fileName, StorageStreamAccess.Read, null, null, null);
        }

        public static StorageStream CreatingNewAndWritting(TransactionalStorage transactionalStorage, IndexStorage indexStorage, StorageCleanupTask cleanup, string fileName, NameValueCollection metadata)
        {
            if (indexStorage == null)
            {
                throw new ArgumentNullException("indexStorage", "indexStorage == null");
            }
            return new StorageStream(transactionalStorage, fileName, StorageStreamAccess.CreateAndWrite, metadata, indexStorage, cleanup);
        }

    	protected StorageStream(TransactionalStorage transactionalStorage, string fileName, StorageStreamAccess storageStreamAccess,
            NameValueCollection metadata, Search.IndexStorage indexStorage, StorageCleanupTask cleanup)
        {
            TransactionalStorage = transactionalStorage;
            StorageStreamAccess = storageStreamAccess;
            Name = fileName;

            switch (storageStreamAccess)
            {
                case StorageStreamAccess.Read:
                    TransactionalStorage.Batch(accessor => fileHeader = accessor.ReadFile(fileName));
                    if (fileHeader.TotalSize == null)
                    {
                        throw new FileNotFoundException("File is not uploaded yet");
                    }
                    Metadata = fileHeader.Metadata;
                    Seek(0, SeekOrigin.Begin);
                    break;
                case StorageStreamAccess.CreateAndWrite:
                    TransactionalStorage.Batch(accessor =>
                    {
						cleanup.IndicateFileToDelete(fileName);
                        accessor.PutFile(fileName, null, metadata);
                        indexStorage.Index(fileName, metadata);
                    });
                    Metadata = metadata;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("storageStreamAccess", storageStreamAccess, "Unknown value");
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    break;
                case SeekOrigin.Current:
                    offset = currentOffset + offset;
                    break;
                case SeekOrigin.End:
                    offset = Length - offset - 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }
            MovePageFrame(offset);
            return currentOffset;
        }

        private void MovePageFrame(long offset)
        {
            offset = Math.Min(Length, offset);
            if (offset < currentPageFrameOffset || fileAndPages == null)
            {
                TransactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile(Name, 0, PagesBatchSize));
                currentPageFrameOffset = 0;
            }
            while (currentPageFrameOffset + currentPageFrameSize - 1 < offset)
            {
                var lastPageFrameSize = currentPageFrameSize;
                var nextPageIndex = fileAndPages.Start + fileAndPages.Pages.Count;
                TransactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile(Name, nextPageIndex, PagesBatchSize));
                if (fileAndPages.Pages.Count < 1)
                {
	                fileAndPages.Start = 0;
                    break;
                }
                currentPageFrameOffset += lastPageFrameSize;
            }
            currentOffset = offset;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (currentOffset >= Length)
            {
                return 0;
            }
			var innerBuffer = new byte[StorageConstants.MaxPageSize];
            var pageOffset = currentPageFrameOffset;
            var length = 0L;
            var startingOffset = currentOffset;
            foreach (var page in fileAndPages.Pages)
            {
                if (pageOffset <= currentOffset && currentOffset < pageOffset + page.Size)
                {
                    var pageLength = 0;
                    TransactionalStorage.Batch(accessor => pageLength = accessor.ReadPage(page.Id, innerBuffer));
                    var sourceIndex = currentOffset - pageOffset;
					length = Math.Min(innerBuffer.Length - sourceIndex,
								   Math.Min(pageLength, Math.Min(buffer.Length - offset, Math.Min(pageLength - sourceIndex, count))));

                    Array.Copy(innerBuffer, sourceIndex, buffer, offset, length);
                    break;
                }
                pageOffset += page.Size;
            }
            MovePageFrame(currentOffset + length);
            return Convert.ToInt32(currentOffset - startingOffset);
        }

        public override void Flush()
        {
			if (innerBuffer != null && innerBufferOffset > 0)
			{
				
				ConcurrencyAwareExecutor.Execute(() => TransactionalStorage.Batch(
					accessor =>
					{
						var hashKey = accessor.InsertPage(innerBuffer, innerBufferOffset);
						accessor.AssociatePage(Name, hashKey, writtingPagePosition, innerBufferOffset);
						writtingPagePosition++;
					}));

				innerBuffer = null;
				innerBufferOffset = 0;
			}
        }

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
					Flush();
				}

				innerOffset += toCopy;
			}
        }

        private int writtingPagePosition = 0;

        public override bool CanRead
        {
            get { return StorageStreamAccess == StorageStreamAccess.Read && fileHeader.TotalSize.HasValue; }
        }

        public override bool CanSeek
        {
            get { return StorageStreamAccess == StorageStreamAccess.Read && fileHeader.TotalSize.HasValue; }
        }

        public override bool CanWrite
        {
            get { return StorageStreamAccess == StorageStreamAccess.CreateAndWrite; }
        }

        public override long Length
        {
            get { return fileHeader.TotalSize ?? 0; }
        }

        public override long Position
        {
            get { return currentOffset; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
					Flush();

                    if (StorageStreamAccess == StorageStreamAccess.CreateAndWrite)
                    {
                        TransactionalStorage.Batch(accessor => accessor.CompleteFileUpload(Name));
                    }
                }
                disposed = true;
            }
        }
    }
}