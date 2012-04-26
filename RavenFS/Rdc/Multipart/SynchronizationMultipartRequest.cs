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
		private readonly string fileName;
		private readonly Stream sourceStream;
		private readonly FileInfoPart fileInfoPart;
		private readonly IList<IFilePart> fileParts;
		private readonly string syncingBoundary;
		private readonly string filesBoundary;

		public SynchronizationMultipartRequest(string destinationUrl, string fileName, NameValueCollection sourceMetadata, Stream sourceStream, IList<RdcNeed> needList)
		{
			this.destinationUrl = destinationUrl;
			this.fileName = fileName;
			this.sourceStream = sourceStream;
			this.syncingBoundary = "syncing";
			this.filesBoundary = "files";

			this.fileInfoPart = new FileInfoPart(fileName, sourceMetadata, syncingBoundary);
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
						result.Add(new SourceFilePart(sourceStream, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength), filesBoundary));
						break;
					case RdcNeedType.Seed:
						result.Add(new SeedFilePart(Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength), filesBoundary));
						break;
					default:
						throw new NotSupportedException();
				}
			}

			return result;
		}

		public Task PushChangesToDesticationAsync()
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

			request.ContentType = "multipart/form-data; boundary=" + syncingBoundary;

			request.Headers[SyncingMultipartConstants.SyncingFileName] = fileName;

			return request.GetRequestStreamAsync()
				.ContinueWith(task =>
								{
									var requestStream = task.Result;

									fileInfoPart.CopyTo(requestStream);

									var filesHeader = new StringBuilder();
									filesHeader.AppendFormat("{0}--{1}{0}", MimeConstants.LineSeparator, syncingBoundary);
									filesHeader.AppendFormat("Content-Disposition: form-data{0}", MimeConstants.LineSeparator);
									filesHeader.AppendFormat("Content-Type: multipart/mixed; boundary={0}{1}", filesBoundary,
															 MimeConstants.LineSeparator);

									byte[] filesHeaderBuffer = Encoding.ASCII.GetBytes(filesHeader.ToString());
									requestStream.Write(filesHeaderBuffer, 0, filesHeaderBuffer.Length);

									foreach (var filePart in fileParts)
									{
										filePart.CopyTo(requestStream);
									}

									var footer = new StringBuilder();
									footer.AppendFormat("{0}--{1}--{0}{0}--{2}--{0}{0}", MimeConstants.LineSeparator, filesBoundary,
														syncingBoundary);

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