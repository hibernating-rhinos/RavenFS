using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Infrastructure;
using RavenFS.Util;

namespace RavenFS.Extensions
{
	using Storage;

	public static class StreamExtensions
    {
		private static Task ReadAsync(this Stream self, byte[] buffer, int start, List<int> reads)
		{
			return self.ReadAsync(buffer, start, buffer.Length - start)
				.ContinueWith(task =>
				{
					reads.Add(task.Result);
					if (task.Result == 0 || task.Result + start >= buffer.Length)
						return task;
					return self.ReadAsync(buffer, start + task.Result, reads);
				})
				.Unwrap();
		}
        private static Task<int> ReadAsync(this Stream self, byte[] buffer, int start)
        {
        	var reads = new List<int>();
        	return self.ReadAsync(buffer, start, reads)
        		.ContinueWith(task =>
        		{
					task.AssertNotFaulted();
        			return reads.Sum();
        		});
        }

    	public static Task<int> ReadAsync(this Stream self, byte[] buffer)
        {
            return self.ReadAsync(buffer, 0);
        }

        public static Task CopyToAsync(this Stream self, Stream destination, long from, long to)
        {            
            var limitedStream = new NarrowedStream(self, from, to);
			return limitedStream.CopyToAsync(destination, StorageConstants.MaxPageSize);
        }

    }
}