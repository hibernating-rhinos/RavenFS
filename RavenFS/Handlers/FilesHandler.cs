using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RavenFS.Infrastructure;
using System.Linq;
using RavenFS.Storage;

namespace RavenFS.Handlers
{
	[HandlerMetadata("/files/?", "GET")]
	public class FilesHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			int start;
			int.TryParse(context.Request.QueryString["start"], out start);

			int pageSize;
			int.TryParse(context.Request.QueryString["pageSize"], out pageSize);

			if (pageSize <= 0 || pageSize >= 256)
				pageSize = 256;

			List<FileHeader> fileHeaders = null;
			Storage.Batch(accessor =>
			{
				fileHeaders = accessor.ReadFiles(start, pageSize).ToList();
			});

			var headersAsJson = JArray.FromObject(fileHeaders);

			var buffer = TakeBuffer();
			try
			{
				int pos;
				using (var memoryStream = new MemoryStream(buffer, true))
				using (var streamWriter = new StreamWriter(memoryStream))
				using (var jsonTextWriter = new JsonTextWriter(streamWriter))
				{
					headersAsJson.WriteTo(jsonTextWriter);

					jsonTextWriter.Flush();
					streamWriter.Flush();

					pos = (int)memoryStream.Position;
				}
				context.Response.ContentType = "application/json";

				return context.Response.OutputStream.WriteAsync(buffer, 0, pos)
					.ContinueWith(task => BufferPool.ReturnBuffer(buffer));
			}
			catch (Exception)
			{
				BufferPool.ReturnBuffer(buffer);
				throw;
			}
		}
	}
}