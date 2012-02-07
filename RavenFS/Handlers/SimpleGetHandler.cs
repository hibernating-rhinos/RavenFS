// //-----------------------------------------------------------------------
// // <copyright company="Hibernating Rhinos LTD">
// //     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// // </copyright>
// //-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Raven.Abstractions.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)", "GET")]
	public class SimpleGetHandler : AbstractAsyncHandler
	{		
		private static readonly Regex startRange = new Regex(@"^bytes=(\d+)-$", RegexOptions.Compiled);

		protected override Task ProcessRequestAsync(HttpContext context)
		{
			context.Response.BufferOutput = false;
			var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;

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

			var totalSize = fileAndPages.TotalSize ?? 0;
			var actualRange = (range ?? 0);
			if(actualRange>totalSize)
			{
				//TODO: handle request beyond the size of the file properly	
			}

			context.Response.AddHeader("Content-Length", Math.Abs(totalSize - actualRange).ToString());

			context.Response.AddHeader("Content-Disposition", "attachment; filename=" + filename);

		    var fileAccessTool = new FileAccessTool(this);

			return fileAccessTool.WriteFile(context.Response.OutputStream, filename, 0, range)
				.ContinueWith(task => task.Result as Task ?? task)
				.Unwrap();
		}		

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
	}
}