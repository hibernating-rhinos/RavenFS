namespace RavenFS.Synchronization.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Threading.Tasks;
	using Newtonsoft.Json;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Util;
	using Rdc.Wrapper;

	public class SynchronizationMultipartRequest
	{
		private readonly string destinationUrl;
		private readonly Guid sourceId;
		private readonly string fileName;
		private readonly NameValueCollection sourceMetadata;
		private readonly Stream sourceStream;
		private readonly IList<RdcNeed> needList;
		private readonly string syncingBoundary;

		public SynchronizationMultipartRequest(string destinationUrl, Guid sourceId, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList)
		{
			this.destinationUrl = destinationUrl;
			this.sourceId = sourceId;
			this.fileName = fileName;
			this.sourceMetadata = sourceMetadata;
			this.sourceStream = sourceStream;
			this.needList = needList;
			this.syncingBoundary = "syncing";
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