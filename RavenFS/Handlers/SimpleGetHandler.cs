using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Raven.Abstractions.Extensions;
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
			var range = GetStartRange(context);

			FileAndPages fileAndPages = null;
			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
			}
			catch (FileNotFoundException)
			{
				context.Response.StatusCode = 404;

				return Completed;
			}

			MetadataExtensions.AddHeaders(context, fileAndPages);

			return WriteFile(context, filename, 0, range);
		}

		private Task WriteFile(HttpContext context, string filename,int fromPage, long? maybeRange)
		{
			FileAndPages fileAndPages = null;
			Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, fromPage, PagesBatchSize));
			var offset = 0;
			var pageIndex = 0;
			if(maybeRange != null)
			{
				var range = maybeRange.Value;
				foreach (var page in fileAndPages.Pages)
				{
					if(page.Size > range)
					{
						offset = (int) range;
						break;
					}
					range -= page.Size;
					pageIndex++;
				}
			}

			if(fileAndPages.Pages.Count == 0)
			{
				return Completed;
			}

			return WritePages(context, fileAndPages.Pages, pageIndex, offset)
				.ContinueWith(task =>
				{
					if (task.Exception != null)
						return task;

					return WriteFile(context, filename,  fromPage + fileAndPages.Pages.Count, null);

				}).Unwrap();
		}

		
		static readonly Regex startRange = new Regex(@"^bytes=(\d+)-$",RegexOptions.Compiled);
		private static long? GetStartRange(HttpContext context)
		{
			var range = context.Request.Headers["Range"];
			if (string.IsNullOrEmpty(range))
				return null;

			var match = startRange.Match(range);

			if (match.Success == false)
				return null;

			long result;
			if (long.TryParse(match.Groups[1].Value, out result) == false)
				return null;

			return result;
		}

		public Task WritePages(HttpContext context, List<PageInformation> pages, int index, int offset)
		{
			return WritePage(context, pages[index], offset)
				.ContinueWith(task =>
				{
					if (task.Exception != null)
						return task;

					if (index+1 >= pages.Count)
						return task;

					return WritePages(context, pages, index + 1, 0);
				})
				.Unwrap();
		}

		private Task WritePage(HttpContext context, PageInformation information, int offset)
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
			return context.Response.OutputStream.WriteAsync(buffer, offset, information.Size - offset)
				.ContinueWith(task =>
				{
					BufferPool.ReturnBuffer(buffer);
					return task;
				})
				.Unwrap();
		}
	}
}