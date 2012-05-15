namespace RavenFS.Rdc.Multipart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Util;

	public class SynchronizationMultipartProcessor
	{
		private readonly string fileName;
		private readonly StorageStream localFile;
		private readonly StorageStream synchronizingFile;
		private readonly IEnumerator<HttpContent> partsEnumerator;
		private readonly TaskCompletionSource<SynchronizationReport> internalProcessingTask = new TaskCompletionSource<SynchronizationReport>();
		private long sourceBytes = 0;
		private long seedBytes = 0;
		private long numberOfFileParts = 0;

		public SynchronizationMultipartProcessor(string fileName, IEnumerator<HttpContent> partsEnumerator, StorageStream localFile, StorageStream synchronizingFile)
		{
			this.fileName = fileName;
			this.partsEnumerator = partsEnumerator;
			this.localFile = localFile;
			this.synchronizingFile = synchronizingFile;
		}

		public Task<SynchronizationReport> ProcessAsync()
		{
			InternalSequentialProcessing();

			return internalProcessingTask.Task;
		}

		private void InternalSequentialProcessing()
		{
			if (!partsEnumerator.MoveNext())
			{
				internalProcessingTask.SetResult(new SynchronizationReport
														{
															FileName = fileName,
															BytesTransfered = sourceBytes,
															BytesCopied = seedBytes,
															NeedListLength = numberOfFileParts
														});
				return;
			}

			var currentPart = partsEnumerator.Current;

			numberOfFileParts += 1;

			var parameters = currentPart.Headers.ContentDisposition.Parameters.ToDictionary(t => t.Name);

			var needType = parameters[SyncingMultipartConstants.NeedType].Value;
			var from = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeFrom].Value);
			var to = Convert.ToInt64(parameters[SyncingMultipartConstants.RangeTo].Value);

			if (needType == "source")
			{
				sourceBytes += (to - from);
				currentPart.CopyToAsync(synchronizingFile)
					.ContinueWith(t => ContinueProcessingIfNotFaulted(t));
			}
			else if (needType == "seed")
			{
				if (localFile == null)
				{
					internalProcessingTask.SetException(
						new SynchronizationException(
							string.Format("Cannot copy a chunk of the file '{0}' on the destination because its stream is uninitialized",
							              fileName)));
					return;
				}

				seedBytes += (to - from);
				localFile.CopyToAsync(synchronizingFile, from, to - 1)
					.ContinueWith(t => ContinueProcessingIfNotFaulted(t));
			}
			else
			{
				internalProcessingTask.SetException(new ArgumentException(string.Format("Invalid need type: '{0}'", needType)));
			}
		}

		private void ContinueProcessingIfNotFaulted(Task task)
		{
			if (task.IsFaulted)
			{
				internalProcessingTask.SetException(task.Exception.InnerExceptions);
			}
			else
			{
				InternalSequentialProcessing();
			}
		}
	}
}