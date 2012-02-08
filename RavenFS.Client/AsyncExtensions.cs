using System;
using System.IO;
using System.Threading.Tasks;

namespace RavenFS.Client
{
	public static class AsyncExtensions
	{
		public static Task CopyToAsync(this Stream source, Stream destination, Action<int> progressReport, int bufferSize = 0x1000)
		{
			return CopyStream(source, destination, progressReport, bufferSize);
		}

		private static Task CopyStream(Stream source, Stream destination, Action<int> progressReport,
		                                       int bufferSize)
		{
		    var listenableStream = new ListenableStream(source);
		    listenableStream.ReadingProgress += (_, progress) => progressReport(progress.Processed);
		    return listenableStream.CopyToAsync(destination, bufferSize);
		}        
	}
}