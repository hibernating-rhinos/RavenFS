// //-----------------------------------------------------------------------
// // <copyright company="Hibernating Rhinos LTD">
// //     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// // </copyright>
// //-----------------------------------------------------------------------
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Util;

namespace RavenFS.Infrastructure
{
	public abstract class AbstractAsyncHandler : IHttpAsyncHandler
	{
		protected Task<object> Completed
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

		private Task ProcessRequestAsync(HttpContext context, AsyncCallback cb)
		{
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

		public Storage.Storage Storage { get; private set; }

		public Regex Url { protected get; set; }

		public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
		{
			return ProcessRequestAsync(context, cb);
		}

		public void EndProcessRequest(IAsyncResult result)
		{
			if (result == null)
				return;
			((Task)result).Dispose();
		}

		public void Initialize(BufferPool bufferPool, Regex url, Storage.Storage storage)
		{
			Url = url;
			BufferPool = bufferPool;
			Storage = storage;
		}
	}
}