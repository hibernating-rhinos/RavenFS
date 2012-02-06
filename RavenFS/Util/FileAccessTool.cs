using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Util
{
    public class FileAccessTool
    {
        protected AbstractAsyncHandler Handler { get; set; }
        private const int PagesBatchSize = 64;

        public FileAccessTool(AbstractAsyncHandler handler)
        {
            Handler = handler;
        }

        public Task<object> WriteFile(Stream output, string filename, int fromPage, long? maybeRange)
        {
            FileAndPages fileAndPages = null;
            Handler.Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, fromPage, PagesBatchSize));
            if (fileAndPages.Pages.Count == 0)
            {
                return Handler.Completed;
            }

            var offset = 0;
            var pageIndex = 0;
            if (maybeRange != null)
            {
                var range = maybeRange.Value;
                foreach (var page in fileAndPages.Pages)
                {
                    if (page.Size > range)
                    {
                        offset = (int)range;
                        break;
                    }
                    range -= page.Size;
                    pageIndex++;
                }

                if (pageIndex >= fileAndPages.Pages.Count)
                {
                    return WriteFile(output, filename, fromPage + fileAndPages.Pages.Count, range);
                }
            }

            return WritePages(output, fileAndPages.Pages, pageIndex, offset)
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                        task.Wait(); // throw 

                    return WriteFile(output, filename, fromPage + fileAndPages.Pages.Count, null);
                }).Unwrap();
        }

        public Task WritePages(Stream output, List<PageInformation> pages, int index, int offset)
        {
            return WritePage(output, pages[index], offset)
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                        return task;

                    if (index + 1 >= pages.Count)
                        return task;

                    return WritePages(output, pages, index + 1, 0);
                })
                .Unwrap();
        }

        private Task WritePage(Stream output, PageInformation information, int offset)
        {
            var buffer = Handler.BufferPool.TakeBuffer(information.Size);
            try
            {
                Handler.Storage.Batch(accessor => accessor.ReadPage(information.Key, buffer));
                return output.WriteAsync(buffer, offset, information.Size - offset)
                    .ContinueWith(task =>
                    {
                        Handler.BufferPool.ReturnBuffer(buffer);
                        return task;
                    })
                    .Unwrap();
            }
            catch (Exception)
            {
                Handler.BufferPool.ReturnBuffer(buffer);
                throw;
            }
        }
    }
}