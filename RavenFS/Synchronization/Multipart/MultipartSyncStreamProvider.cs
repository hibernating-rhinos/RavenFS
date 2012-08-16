namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using Client;
	using NLog;
	using Storage;
	using Util;

	public class MultipartSyncStreamProvider : MultipartStreamProvider
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private class BodyPartInfo
		{
			public string Type { get; set; }

			public List<PageInformation> UnassignedPages { get; set; }

			public long From { get; set; }

			public long To { get; set; }
		}

		private readonly SynchronizingFileStream synchronizingFile;
		private readonly StorageStream localFile;
		private readonly TransactionalStorage storage;

		private readonly List<BodyPartInfo> bodyParts = new List<BodyPartInfo>();

		public long BytesTransfered { get; private set; }
		public long BytesCopied { get; private set; }
		public long NumberOfFileParts { get; private set; }

		public MultipartSyncStreamProvider(SynchronizingFileStream synchronizingFile, StorageStream localFile, TransactionalStorage storage)
		{
			this.synchronizingFile = synchronizingFile;
			this.localFile = localFile;
			this.storage = storage;

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
				var length = (to - from + 1);

				if (length <= 0) // it might happen that synchronized file is empty, so there will have no pages
				{
					bodyParts.Add(new BodyPartInfo { Type = "source", UnassignedPages = new List<PageInformation>(), From = from, To = to});
				}
				else
				{
					bodyParts.Add(new BodyPartInfo { Type = "source", From = from, To = to});
				}

				return synchronizingFile;
			}

			if (needType == "seed")
			{
				bodyParts.Add(new BodyPartInfo { Type = "seed", From = from, To = to});

				RetrieveLastWrittenPages("source");

				return new MemoryStream(); // we can return MemoryStream because 'seed' part is always empty
			}

			throw new ArgumentException(string.Format("Invalid need type: '{0}'", needType));
		}

		/// <summary>
		/// Copies local file chunks ('seed' parts) and associate pages to right order
		/// </summary>
		/// <returns>Task representing copy and association operations</returns>
		public override Task ExecutePostProcessingAsync()
		{
			return Task.Factory.StartNew(() =>
			{
				log.Info(
					"Multipart synchronization request of a file '{0}' has been parsed. Starting to copy local file chunks and associate pages",
					synchronizingFile.Name);

				RetrieveLastWrittenPages("source");

				int writtingPagePosition = 0;

				uint numberOfCopiedLocalFileParts = 0;

				foreach (var body in bodyParts)
				{
					if(body.Type == "seed") // copy part from local file
					{
						if (localFile == null)
						{
							throw new SynchronizationException("Cannot copy a chunk of the local file because its stream is uninitialized");
						}

						var limitedStream = new NarrowedStream(localFile, body.From, body.To);
						limitedStream.CopyTo(synchronizingFile);
						synchronizingFile.Dispose();

						numberOfCopiedLocalFileParts++;

						RetrieveLastWrittenPages("seed");
					}

					foreach (var page in body.UnassignedPages) // assign pages
					{
						storage.Batch(accessor => accessor.AssociatePage(synchronizingFile.Name, page.Id, writtingPagePosition, page.Size));
						writtingPagePosition++;

						// calculate transferred and copied bytes
						if(body.Type == "seed")
						{
							BytesCopied += page.Size;
						}
						else if (body.Type == "source")
						{
							BytesTransfered += page.Size;
						}
					}	
				}

				log.Info("Operation of copy {0} local file chunks and pages association for a file '{1}' has finished", numberOfCopiedLocalFileParts, synchronizingFile.Name);
			});
		}

		private void RetrieveLastWrittenPages(string type)
		{
			var firstUnassignedPage = bodyParts.FirstOrDefault(x => x.Type == type && x.UnassignedPages == null);

			if (firstUnassignedPage != null && synchronizingFile.LastWrittenPages.Count > 0)
			{
				firstUnassignedPage.UnassignedPages = new List<PageInformation>(synchronizingFile.LastWrittenPages);
				synchronizingFile.LastWrittenPages.Clear();
			}
		}
	}
}
