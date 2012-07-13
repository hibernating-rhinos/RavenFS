namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using Client;
	using Infrastructure;
	using Util;

	public class MultipartSyncStreamProvider : MultipartStreamProvider
	{
		private readonly SynchronizingFileStream synchronizingFile;
		private readonly StorageStream localFile;

		public long BytesTransfered { get; private set; }
		public long BytesCopied { get; private set; }
		public long NumberOfFileParts { get; private set; }

		public MultipartSyncStreamProvider(SynchronizingFileStream synchronizingFile, StorageStream localFile)
		{
			this.synchronizingFile = synchronizingFile;
			this.localFile = localFile;

			BytesTransfered = BytesCopied = NumberOfFileParts = 0;
		}

		public SynchronizationReport SynchronizationReport { get; set; }

		public override Stream GetStream(HttpContent parent, HttpContentHeaders headers)
		{
			if (parent == null)
			{
				throw new ArgumentNullException("parent");
			}

			if (headers == null)
			{
				throw new ArgumentNullException("headers");
			}

			var parameters = headers.ContentDisposition.Parameters.ToDictionary(t => t.Name);

			var needType = parameters[SyncingMultipartConstants.NeedType].Value;
			var from = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeFrom].Value);
			var to = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeTo].Value);

			NumberOfFileParts++;

			if (needType == "source")
			{
				var expectedLength = (to - from + 1);

				BytesTransfered += expectedLength;

				return synchronizingFile;
			}

			if (needType == "seed")
			{
				if (localFile == null)
				{
					throw new SynchronizationException("Cannot copy a chunk of the local file because its stream is uninitialized");
				}

				var limitedStream = new NarrowedStream(localFile, from, to);
				limitedStream.CopyTo(synchronizingFile, StorageStream.MaxPageSize);

				BytesCopied += (to - from + 1);

				return new MemoryStream();
			}

			throw new ArgumentException(string.Format("Invalid need type: '{0}'", needType));
		}
	}
}
