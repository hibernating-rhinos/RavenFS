using System;
using System.IO;
using System.Threading.Tasks;

namespace RavenFS.Client
{
	public static class AsyncExtensions
	{
		public static Task CopyToAsync(this Stream source, Stream destination, Action<int> progressReport, int bufferSize = 0x1000)
		{
			byte[] buffer = new byte[bufferSize];

			return CopyStream(source, destination, progressReport, buffer, 0);
		}

		private static Task CopyStream(Stream source, Stream destination, Action<int> progressReport,
		                                       byte[] buffer, int progress)
		{
			return source.ReadAsync(buffer, 0, buffer.Length)
				.ContinueWith(readTask =>
				{
					if (readTask.Result == 0)
						return readTask; // done, nothing more to do.

					progress += readTask.Result;
					return destination.WriteAsync(buffer, 0, readTask.Result)
						.ContinueWith(writeTask =>
						{
							progressReport(progress);
							return CopyStream(source, destination, progressReport, buffer, progress);
						}).Unwrap();
				}).Unwrap();
		}
	}
}