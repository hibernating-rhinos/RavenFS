namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using Newtonsoft.Json;
	using RavenFS.Util;
	using Rdc.Wrapper;
	using Storage;

	public class SynchronizationMultipartRequest
	{
		private readonly TransactionalStorage storage;
		private readonly string destinationUrl;
		private readonly Guid sourceId;
		private readonly string fileName;
		private readonly NameValueCollection sourceMetadata;
		private readonly Stream sourceStream;
		private readonly IList<RdcNeed> needList;
		private readonly TransferredChangesType transferredChangesType;
		private readonly string syncingBoundary;
		private readonly List<PageRange> pageRanges;

		public SynchronizationMultipartRequest(TransactionalStorage storage, string destinationUrl, Guid sourceId, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList, TransferredChangesType transferredChangesType)
		{
			this.storage = storage;
			this.destinationUrl = destinationUrl;
			this.sourceId = sourceId;
			this.fileName = fileName;
			this.sourceMetadata = sourceMetadata;
			this.sourceStream = sourceStream;
			this.needList = needList;
			this.transferredChangesType = transferredChangesType;
			this.syncingBoundary = "syncing";

			if (transferredChangesType == TransferredChangesType.Pages)
			{
				pageRanges = TransformToPageRangeParts(needList);
			}
		}

		public async Task<SynchronizationReport> PushChangesAsync()
		{
			if (sourceStream.CanRead == false)
			{
				throw new AggregateException("Stream does not support reading");
			}

			var request = (HttpWebRequest)WebRequest.Create(destinationUrl + "/synchronization/MultipartProceed");
			request.Method = "POST";
			request.SendChunked = true;
			request.AllowWriteStreamBuffering = false;
			request.KeepAlive = true;

			request.AddHeaders(sourceMetadata);

			request.ContentType = "multipart/form-data; boundary=" + syncingBoundary;

			request.Headers[SyncingMultipartConstants.FileName] = fileName;
			request.Headers[SyncingMultipartConstants.SourceServerId] = sourceId.ToString();
			request.Headers[SyncingMultipartConstants.TransferredChanges] = transferredChangesType.ToString();

			try
			{
				using (var requestStream = await request.GetRequestStreamAsync())
				{
					await PrepareMultipartContent().CopyToAsync(requestStream);

					using (var respose = await request.GetResponseAsync())
					{
						using (var responseStream = respose.GetResponseStream())
						{
							return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(responseStream)));
						}
					}
				}
			}
			catch (WebException exception)
			{
				throw exception.BetterWebExceptionError();
			}
		}

		internal MultipartContent PrepareMultipartContent()
		{
			var content = new MultipartContent("form-data", syncingBoundary);

			if (transferredChangesType == TransferredChangesType.Bytes)
			{
				foreach (var item in needList)
				{
					long @from = Convert.ToInt64(item.FileOffset);
					long length = Convert.ToInt64(item.BlockLength);
					long to = from + length - 1;

					switch (item.BlockType)
					{
						case RdcNeedType.Source:
							content.Add(new SourceFilePart(new NarrowedStream(sourceStream, from, to)));
							break;
						case RdcNeedType.Seed:
							content.Add(new SeedFilePart(@from, to));
							break;
						default:
							throw new NotSupportedException();
					}
				}
			}
			else if (transferredChangesType == TransferredChangesType.Pages)
			{
				foreach (var item in pageRanges)
				{
					if (item.OrderedPages.FirstOrDefault() != null && item.OrderedPages.LastOrDefault() != null)
					{
						content.Add(new SourceFilePart(new NarrowedStream(sourceStream, item.StartByte, item.EndByte)));
					}
					else
					{
						content.Add(new SeedFilePart(item.StartByte, item.EndByte));
					}
				}
			}

			return content;
		}

		private List<PageRange> TransformToPageRangeParts(IEnumerable<RdcNeed> needs)
		{
			var overlapingPageRanges = new List<PageRange>();

			foreach (var need in needs)
			{
				long @from = Convert.ToInt64(need.FileOffset);
				long length = Convert.ToInt64(need.BlockLength);
				long to = from + length - 1;

				PageRange pageRange = null;

				if (need.BlockType == RdcNeedType.Source)
				{
					storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes(fileName, from, to));
				}
				else if (need.BlockType == RdcNeedType.Seed)
				{
					pageRange = new PageRange { StartByte = @from, EndByte = to };
				}

				if (pageRange != null)
				{
					overlapingPageRanges.Add(pageRange);
				}
			}

			var finalPageRanges = new List<PageRange>();

			foreach (var overlapingPageRange in overlapingPageRanges)
			{
				var lastPageRange = finalPageRanges.LastOrDefault(x => x.OrderedPages.FirstOrDefault() != null);

				if (overlapingPageRange.OrderedPages.FirstOrDefault() == null || finalPageRanges.Count == 0 || lastPageRange == null || !lastPageRange.IsOverlaping(overlapingPageRange))
				{
					finalPageRanges.Add(overlapingPageRange);
				}
				else
				{
					lastPageRange.Add(overlapingPageRange);
				}
			}

			return finalPageRanges;
		}
	}
}