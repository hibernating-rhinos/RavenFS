namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using Client;
	using Newtonsoft.Json;
	using Util;
	using Wrapper;

	public class SynchronizationMultipartRequest
	{
		private readonly string destinationUrl;
		private readonly string sourceUrl;
		private readonly string fileName;
		private readonly NameValueCollection sourceMetadata;
		private readonly Stream sourceStream;
		private readonly IList<RdcNeed> needList;
		private readonly string syncingBoundary;

		public SynchronizationMultipartRequest(string destinationUrl, string sourceUrl, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList)
		{
			this.destinationUrl = destinationUrl;
			this.sourceUrl = sourceUrl;
			this.fileName = fileName;
			this.sourceMetadata = sourceMetadata;
			this.sourceStream = sourceStream;
			this.needList = needList;
			this.syncingBoundary = "syncing";
		}

		private static void AddHeaders(NameValueCollection metadata, HttpWebRequest request)
		{
			foreach (var key in metadata.AllKeys)
			{
				var values = metadata.GetValues(key);
				if (values == null)
					continue;
				foreach (var value in values)
				{
					request.Headers[key] = value;
				}
			}
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

			AddHeaders(sourceMetadata, request);

			request.ContentType = "multipart/form-data; boundary=" + syncingBoundary;

			request.Headers[SyncingMultipartConstants.FileName] = fileName;
			request.Headers[SyncingMultipartConstants.SourceServerUrl] = sourceUrl;

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
						content.Add(new SourceFilePart(new NarrowedStream(sourceStream, from, to)));
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