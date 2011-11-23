// //-----------------------------------------------------------------------
// // <copyright company="Hibernating Rhinos LTD">
// //     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// // </copyright>
// //-----------------------------------------------------------------------
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using RavenFS.Util;

namespace RavenFS.Infrastructure
{
	public abstract class AbstractAsyncHandler : IHttpAsyncHandler
	{
		public Task<object> Completed
		{
			get
			{
				var taskCompletionSource = new TaskCompletionSource<object>();
				taskCompletionSource.SetResult(null);
				return taskCompletionSource.Task;
			}
		}

		protected abstract Task ProcessRequestAsync(HttpContext context);

		public IBufferPool BufferPool { get; set; }

		public byte[] TakeBuffer()
		{
			return BufferPool.TakeBuffer(64 * 1024);
		}

		protected Tuple<int,int> Paging(HttpContext context)
		{
			int start;
			int.TryParse(context.Request.QueryString["start"], out start);

			int pageSize;
			int.TryParse(context.Request.QueryString["pageSize"], out pageSize);

			if (pageSize <= 0 || pageSize >= 256)
				pageSize = 256;

			return Tuple.Create(start, pageSize);
		}

		protected Task WriteJson(HttpContext context, object obj)
		{
			var buffer = TakeBuffer();
			try
			{
				int pos;
				using (var memoryStream = new MemoryStream(buffer, true))
				using (var streamWriter = new StreamWriter(memoryStream))
				using (var jsonTextWriter = new JsonTextWriter(streamWriter))
				{
					JsonSerializerFactory.Create().Serialize(jsonTextWriter, obj);

					jsonTextWriter.Flush();
					streamWriter.Flush();

					pos = (int)memoryStream.Position;
				}
				context.Response.ContentType = "application/json";
				context.Response.Expires = -1; // we don't allow caching of the json responses
				return context.Response.OutputStream.WriteAsync(buffer, 0, pos)
					.ContinueWith(task => BufferPool.ReturnBuffer(buffer));
			}
			catch (Exception)
			{
				BufferPool.ReturnBuffer(buffer);
				throw;
			}
		}


		private Task ProcessRequestAsync(HttpContext context, AsyncCallback cb)
		{
			context.Response.TrySkipIisCustomErrors = true;
			return ProcessRequestAsync(context)
				.ContinueWith(task => cb(task));
		}

		public void ProcessRequest(HttpContext context)
		{
			ProcessRequestAsync(context).Wait();
		}

		public bool IsReusable
		{
			get { return true; }
		}

		public Storage.TransactionalStorage Storage { get; private set; }

		public Search.IndexStorage Search { get; private set; }

		public Regex Url { protected get; set; }

		public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
		{
			return ProcessRequestAsync(context, cb);
		}

		public void EndProcessRequest(IAsyncResult result)
		{
			if (result == null)
				return;

			var task = ((Task)result);
			task.Wait();
			task.Dispose();
		}

		public void Initialize(BufferPool bufferPool, Regex url, Storage.TransactionalStorage storage, Search.IndexStorage search)
		{
			Url = url;
			BufferPool = bufferPool;
			Storage = storage;
			Search = search;
		}
	}
}