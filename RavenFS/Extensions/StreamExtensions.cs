using System;
using System.IO;
using System.Threading.Tasks;

namespace RavenFS.Extensions
{
	public static class StreamExtensions
	{
		private static Task<int> ReadAsync(this Stream stream, byte[] buffer, int start)
		{
			return stream.ReadAsync(buffer, start, buffer.Length - start)
				.ContinueWith(task =>
				{
					if (task.Result == 0)
						return task;
					if (task.Result < buffer.Length)
					{
						return stream.ReadAsync(buffer, start + task.Result);
					}
					return task;
				})
                .Unwrap()
				.ContinueWith(task => task.Result == 0 ? start : task.Result);
		}
        
		public static Task<int> ReadAsync(this Stream stream, byte[] buffer)
		{
			return stream.ReadAsync(buffer, 0);
		}
	}
}