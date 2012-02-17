using System;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Util;

namespace RavenFS.Extensions
{
    public static class StreamExtensions
    {
        private static Task<int> ReadAsync(this Stream self, byte[] buffer, int start)
        {
            return self.ReadAsync(buffer, start, buffer.Length - start)
                .ContinueWith(task =>
                {
                    if (task.Result == 0)
                        return task;
                    if (task.Result < buffer.Length)
                    {
                        return self.ReadAsync(buffer, start + task.Result);
                    }
                    return task;
                })
                .Unwrap()
                .ContinueWith(task => task.Result == 0 ? start : task.Result);
        }

        public static Task<int> ReadAsync(this Stream self, byte[] buffer)
        {
            return self.ReadAsync(buffer, 0);
        }

        public static Task CopyToAsync(this Stream self, Stream destination, long from, long to)
        {            
            var limitedStream = new NarrowedStream(self, from, to);
            return limitedStream.CopyToAsync(destination, StorageStream.MaxPageSize);
        }

    }
}