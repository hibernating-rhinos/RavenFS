using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Web;
using RavenFS.Search;
using RavenFS.Storage;
using System.Security.AccessControl;

namespace RavenFS.Util
{
    public class StorageStream : Stream
    {
        public TransactionalStorage TransactionalStorage { get; private set; }
        public StorageStreamAccess StorageStreamAccess { get; private set; }

        private FileHeader fileHeader;
        public string Name { get; private set; }

        public NameValueCollection Metadata { get; private set; }

        public const int MaxPageSize = 64 * 1024;
        private const int PagesBatchSize = 64;
        private FileAndPages fileAndPages;
        private long currentOffset;
        private long currentPageFrameSize { get { return fileAndPages.Pages.Sum(item => item.Size); } }
        private long currentPageFrameOffset;
        private bool disposed = false;

        public static StorageStream Reading(TransactionalStorage transactionalStorage, string fileName)
        {
            return new StorageStream(transactionalStorage, fileName, StorageStreamAccess.Read, null, null);
        }

        public static StorageStream CreatingNewAndWritting(TransactionalStorage transactionalStorage, IndexStorage indexStorage, string fileName, NameValueCollection metadata)
        {
            Contract.Requires<ArgumentNullException>(indexStorage != null, "indexStorage == null");
            return new StorageStream(transactionalStorage, fileName, StorageStreamAccess.CreateAndWrite, metadata, indexStorage);
        }

        private StorageStream(TransactionalStorage transactionalStorage, string fileName, StorageStreamAccess storageStreamAccess,
            NameValueCollection metadata, Search.IndexStorage indexStorage)
        {
            Contract.Requires<ArgumentNullException>(transactionalStorage != null, "transactionalStorage == null");

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
                        accessor.Delete(fileName);
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
                var nextPageIndex = fileAndPages.Start + fileAndPages.Pages.Count;
                TransactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile(Name, nextPageIndex, PagesBatchSize));
                if (fileAndPages.Pages.Count < 1)
                {
                    break;
                }
                currentPageFrameOffset += currentPageFrameSize;
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
            var innerBuffer = new byte[MaxPageSize];
            var pageOffset = currentPageFrameOffset;
            var length = 0L;
            var startingOffset = currentOffset;
            foreach (var page in fileAndPages.Pages)
            {
                if (pageOffset <= currentOffset && currentOffset < pageOffset + page.Size)
                {
                    var pageLength = 0;
                    TransactionalStorage.Batch(accessor => pageLength = accessor.ReadPage(page.Key, innerBuffer));
                    var sourceIndex = currentOffset - pageOffset;
                    length = Math.Min(innerBuffer.Length - sourceIndex, Math.Min(pageLength, Math.Min(buffer.Length - offset, count)));

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
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var innerOffset = 0;
            var innerBuffer = new byte[MaxPageSize];
            while (innerOffset < count)
            {
                var toCopy = Math.Min(MaxPageSize, count - innerOffset);
                if (toCopy == 0)
                {
                    throw new Exception("Impossible");
                }
                Array.Copy(buffer, offset + innerOffset, innerBuffer, 0, toCopy);
                TransactionalStorage.Batch(
                    accessor =>
                    {
                        var hashKey = accessor.InsertPage(innerBuffer, toCopy);
                        accessor.AssociatePage(Name, hashKey, writtingPagePosition, toCopy);
                    });
                innerOffset += toCopy;
                writtingPagePosition++;
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
                    if (StorageStreamAccess == StorageStreamAccess.CreateAndWrite)
                    {
                        TransactionalStorage.Batch(accessor => accessor.CompleteFileUpload(Name));
                    }
                }
                disposed = true;
            }
        }
    }

    public enum StorageStreamAccess
    {
        Read,
        CreateAndWrite
    }
}