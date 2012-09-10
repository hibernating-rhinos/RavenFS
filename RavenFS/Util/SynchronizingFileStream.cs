namespace RavenFS.Util
{
	using System.Collections.Specialized;
	using System.Security.Cryptography;
	using Extensions;
	using Search;
	using Storage;

	public class SynchronizingFileStream : StorageStream
	{
		private readonly MD5 md5Hasher;

		private SynchronizingFileStream(TransactionalStorage transactionalStorage, string fileName, StorageStreamAccess storageStreamAccess, NameValueCollection metadata, IndexStorage indexStorage) : base(transactionalStorage, fileName, storageStreamAccess, metadata, indexStorage)
		{
			md5Hasher = new MD5CryptoServiceProvider();
		}

		public bool PreventUploadComplete { get; set; }

		public string FileHash { get; private set; }

		public override void Flush()
		{
			if (innerBuffer != null && innerBufferOffset > 0)
			{
				md5Hasher.TransformBlock(innerBuffer, 0, innerBufferOffset, null, 0);
				base.Flush();
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (!PreventUploadComplete)
			{
				base.Dispose(disposing);

				md5Hasher.TransformFinalBlock(new byte[0], 0, 0);
				FileHash = md5Hasher.Hash.ToStringHash();
				md5Hasher.Dispose();
			}
		}

		public static SynchronizingFileStream CreatingOrOpeningAndWritting(TransactionalStorage storage, IndexStorage search, string fileName, NameValueCollection metadata)
		{
			return new SynchronizingFileStream(storage, fileName, StorageStreamAccess.CreateAndWrite, metadata, search)
			       	{ PreventUploadComplete = true };
		}
	}
}