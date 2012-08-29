namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using NLog;
	using Storage;
	using Util;

	public class MultipartPageSyncStreamProvider : MultipartSyncStreamProvider
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private class BodyPartInfo
		{
			public string Type { get; set; }

			public PageRange PageRange { get; set; }
		}

		private readonly List<BodyPartInfo> bodies = new List<BodyPartInfo>();

		public MultipartPageSyncStreamProvider(SynchronizingFileStream synchronizingFile, StorageStream localFile, TransactionalStorage storage)
			: base(synchronizingFile, localFile,storage)
		{

		}

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

			NumberOfFileParts++;

			if (needType == "source")
			{
				var byteFrom = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeFrom].Value);
				var byteTo = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeTo].Value);

				var pageRange = new PageRange
				                	{
				                		StartByte = byteFrom,
				                		EndByte = byteTo
				                	};

				bodies.Add(new BodyPartInfo { Type = "source", PageRange = pageRange});
				RetrieveLastWrittenPages("source");
				return synchronizingFile;
			}

			if (needType == "seed")
			{
				var from = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeFrom].Value);
				var to = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeTo].Value);

				PageRange pageRange = null;

				storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes(localFile.Name, from, to));
				if (pageRange != null)
				{
					bodies.Add(new BodyPartInfo { Type = "seed", PageRange = pageRange});
				}

				RetrieveLastWrittenPages("source");

				return new MemoryStream(); // we can return MemoryStream because 'seed' part is always empty
			}

			throw new ArgumentException(string.Format("Invalid need type: '{0}'", needType));
		}

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

				foreach (var body in bodies)
				{
					if(body.Type == "seed") // copy part from local file
					{
						if (body.PageRange != null)
						{
							FileAndPages fileAndPages = null;

							var start = body.PageRange.OrderedPages.First().Id - 1;
							var pagesToLoad = body.PageRange.OrderedPages.Last().Id - body.PageRange.OrderedPages.First().Id + 1;

							storage.Batch(accessor => fileAndPages = accessor.GetFile(localFile.Name, start, pagesToLoad));
							body.PageRange.OrderedPages = fileAndPages.Pages;
						}

						numberOfCopiedLocalFileParts++;
					}

					foreach (var page in body.PageRange.OrderedPages) // assign pages
					{
						storage.Batch(accessor => accessor.AssociatePage(synchronizingFile.Name, page.Id, writtingPagePosition, page.Size));
						writtingPagePosition++;

						// calculate transferred and copied bytes
						if(body.Type == "seed")
						{
							storage.Batch(accessor => accessor.IncreasePageUsageCount(page.Id));
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
			var firstUnassignedPage = bodies.FirstOrDefault(x => x.Type == type && x.PageRange.OrderedPages.FirstOrDefault() == null);

			if (firstUnassignedPage != null && synchronizingFile.LastWrittenPages.Count > 0)
			{
				firstUnassignedPage.PageRange.OrderedPages = new List<PageInformation>(synchronizingFile.LastWrittenPages);
				synchronizingFile.LastWrittenPages.Clear();
			}
		}
	}
}
