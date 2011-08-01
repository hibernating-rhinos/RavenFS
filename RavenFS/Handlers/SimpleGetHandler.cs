using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)","GET")]
	public class SimpleGetHandler : AbstractAsyncHandler
	{
		private const int PagesBatchSize = 64;

		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var filename = Url.Match(context.Request.Url.AbsolutePath).Groups[1].Value;

			return WriteFile(context, filename, 0);
		}

		private Task WriteFile(HttpContext context, string filename, int fromPage)
		{
			FileInformation fileInformation = null;
			try
			{
				Storage.Batch(accessor => fileInformation = accessor.GetFile(filename, fromPage, PagesBatchSize));
			}
			catch (FileNotFoundException)
			{
				context.Response.Status = "Not Found";
				context.Response.StatusCode = 404;

				return Completed;
			}

			foreach (var key in fileInformation.Metadata.AllKeys)
			{
				var values = fileInformation.Metadata.GetValues(key);
				if (values == null)
					continue;

				foreach (var value in values)
				{
				context.Response.AddHeader(key, value);
					
				}
			}

			if(fileInformation.Pages.Count == 0)
			{
				if(fromPage == 0)
				{
					context.Response.StatusCode = 204;
					context.Response.Status = "No Content";
				}
				return Completed;
			}

			return WritePages(context, fileInformation.Pages, 0)
				.ContinueWith(task =>
				{
					if (task.Exception != null)
						return task;

					if (fileInformation.Pages.Count != PagesBatchSize)
						return task;

					return WriteFile(context, filename, fromPage + PagesBatchSize);

				}).Unwrap();
		}

		public Task WritePages(HttpContext context, List<PageInformation> pages, int index)
		{
			return WritePage(context, pages[index])
				.ContinueWith(task =>
				{
					if (task.Exception != null)
						return task;

					if (index >= pages.Count)
						return task;

					return WritePages(context, pages, index + 1);
				})
				.Unwrap();
		}

		private Task WritePage(HttpContext context, PageInformation information)
		{
			var buffer = BufferPool.TakeBuffer(information.Size);
			try
			{
				Storage.Batch(accessor => accessor.ReadPage(information.Key, buffer));
			}
			catch (Exception)
			{
				BufferPool.ReturnBuffer(buffer);
				throw;
			}
			return context.Response.OutputStream.WriteAsync(buffer, 0, information.Size)
				.ContinueWith(task =>
				{
					BufferPool.ReturnBuffer(buffer);
					return task;
				})
				.Unwrap();
		}
	}
}