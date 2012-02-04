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
				.ContinueWith(task => task.Result);
		}

		public static Task<int> ReadAsync(this Stream stream, byte[] buffer)
		{
			return stream.ReadAsync(buffer, 0);
		}

        public static Task<long> ReadAsync(this Stream stream, byte[] buffer, Action<byte[], int> everyRead)
        {
            var result = new Task<long>(() =>
            {
                var allRead = 0L;
                int read;
                do
                {
                    read = 0;
                    int lastRead;
                    do
                    {
                        lastRead = stream.Read(buffer, read, buffer.Length - read);
                        read += lastRead;
                    } while (lastRead != 0 && read < buffer.Length);
                    everyRead(buffer, read);
                    allRead += read;
                } while (read != 0);
                return allRead;
            });
            result.Start();
            return result;
        }
	}
}