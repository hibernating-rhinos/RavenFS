namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
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
		private readonly string destinationUrl;
		private readonly Guid sourceId;
		private readonly string fileName;
		private readonly NameValueCollection sourceMetadata;
		private readonly Stream sourceStream;
		private readonly IList<RdcNeed> needList;
		private readonly TransactionalStorage storage;
		private readonly string syncingBoundary;

		public SynchronizationMultipartRequest(string destinationUrl, Guid sourceId, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList, TransactionalStorage storage)
		{
			this.destinationUrl = destinationUrl;
			this.sourceId = sourceId;
			this.fileName = fileName;
			this.sourceMetadata = sourceMetadata;
			this.sourceStream = sourceStream;
			this.needList = needList;
			this.storage = storage;
			this.syncingBoundary = "syncing";
		}

		public Task<SynchronizationReport> PushChangesAsync()
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

			return request.GetRequestStreamAsync()
				.ContinueWith(task => PrepareMultipartContent().CopyToAsync(task.Result)
					.ContinueWith(t => task.Result.Close()))
				.Unwrap()
				.ContinueWith(task =>
				              	{
				              		task.Wait();
									return request.GetResponseAsync();
				              	})
				.Unwrap()
				.ContinueWith(task =>
			                {
			                    using (var stream = task.Result.GetResponseStream())
			                    {
			                        return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
			                    }
			                })
				.TryThrowBetterError();
		}

		internal MultipartContent PrepareMultipartContent()
		{
			var content = new MultipartContent("form-data", syncingBoundary);

			foreach (var item in needList)
			{
				long @from = Convert.ToInt64(item.FileOffset);
				long length = Convert.ToInt64(item.BlockLength);
				long to = from + length - 1;

				switch (item.BlockType)
				{
					case RdcNeedType.Source:
						PageRange pageRange = null;

						storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes(fileName, @from, to));
						content.Add(new SourceFilePart(new NarrowedStream(sourceStream, pageRange.StartByte, pageRange.EndByte), pageRange));
						break;
					case RdcNeedType.Seed:
						content.Add(new SeedFilePart(@from, to));
						break;
					default:
						throw new NotSupportedException();
				}
			}

			return content;
		}
	}
}