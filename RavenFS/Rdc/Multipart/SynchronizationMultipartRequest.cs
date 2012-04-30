namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Text;
	using System.Threading.Tasks;
	using Client;
	using Wrapper;

	public class SynchronizationMultipartRequest
	{
		private readonly string destinationUrl;
		private readonly string sourceUrl;
		private readonly string fileName;
		private readonly NameValueCollection sourceMetadata;
		private readonly Stream sourceStream;
		private readonly IList<IFilePart> fileParts;
		private readonly string syncingBoundary;

		public SynchronizationMultipartRequest(string destinationUrl, string sourceUrl, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList)
		{
			this.destinationUrl = destinationUrl;
			this.sourceUrl = sourceUrl;
			this.fileName = fileName;
			this.sourceMetadata = sourceMetadata;
			this.sourceStream = sourceStream;
			this.syncingBoundary = "syncing";

			this.fileParts = CreateFileParts(needList);
		}

		private IList<IFilePart> CreateFileParts(IList<RdcNeed> needList)
		{
			var result = new List<IFilePart>();

			foreach (var item in needList)
			{
				switch (item.BlockType)
				{
					case RdcNeedType.Source:
						result.Add(new SourceFilePart(sourceStream, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength), syncingBoundary));
						break;
					case RdcNeedType.Seed:
						result.Add(new SeedFilePart(Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength), syncingBoundary));
						break;
					default:
						throw new NotSupportedException();
				}
			}

			return result;
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

		public Task PushChangesAsync()
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
				.ContinueWith(task =>
								{
									var requestStream = task.Result;

									foreach (var filePart in fileParts)
									{
										filePart.CopyTo(requestStream);
									}

									var footer = new StringBuilder();
									footer.AppendFormat("{0}--{1}--{0}{0}", MimeConstants.LineSeparator, syncingBoundary);

									var footerBuffer = Encoding.ASCII.GetBytes(footer.ToString());
									task.Result.Write(footerBuffer, 0, footerBuffer.Length);

									return task;
								})
				.Unwrap()
				.ContinueWith(task => task.Result.Close())
				.ContinueWith(task =>
				{
					task.Wait();
					return request.GetResponseAsync();
				})
				.Unwrap()
				.ContinueWith(task => task.Result.Close())
				.TryThrowBetterError();
		}
	}
}