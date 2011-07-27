// //-----------------------------------------------------------------------
// // <copyright company="Hibernating Rhinos LTD">
// //     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// // </copyright>
// //-----------------------------------------------------------------------
using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Handlers
{
	public abstract class AbstractAsyncHandler : IHttpAsyncHandler
	{
		protected abstract Task ProcessRequestAsync(HttpContext context);

		public IStorage Storage { get; set; }
		public IBufferPool BufferPool { get; set; }
	
		public byte[] TakeBuffer()
		{
			return BufferPool.TakeBuffer(64*1024);
		}

		private Task ProcessRequestAsync(HttpContext context, AsyncCallback cb)
		{
			return ProcessRequestAsync(context)
				.ContinueWith(task => cb(task));
		}


		#region IHttpAsyncHandler Members

		/// <summary>
		///   Enables processing of HTTP Web requests by a custom HttpHandler that implements the <see cref = "T:System.Web.IHttpHandler" /> interface.
		/// </summary>
		/// <param name = "context">An <see cref = "T:System.Web.HttpContext" /> object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests. </param>
		public void ProcessRequest(HttpContext context)
		{
			ProcessRequestAsync(context).Wait();
		}

		/// <summary>
		///   Gets a value indicating whether another request can use the <see cref = "T:System.Web.IHttpHandler" /> instance.
		/// </summary>
		/// <returns>
		///   true if the <see cref = "T:System.Web.IHttpHandler" /> instance is reusable; otherwise, false.
		/// </returns>
		public bool IsReusable
		{
			get { return true; }
		}

		public Regex Url { protected get; set; }

		/// <summary>
		///   Initiates an asynchronous call to the HTTP handler.
		/// </summary>
		/// <returns>
		///   An <see cref = "T:System.IAsyncResult" /> that contains information about the status of the process.
		/// </returns>
		/// <param name = "context">An <see cref = "T:System.Web.HttpContext" /> object that provides references to intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests. </param>
		/// <param name = "cb">The <see cref = "T:System.AsyncCallback" /> to call when the asynchronous method call is complete. If <paramref name = "cb" /> is null, the delegate is not called. </param>
		/// <param name = "extraData">Any extra data needed to process the request. </param>
		public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
		{
			return ProcessRequestAsync(context, cb);
		}


		/// <summary>
		///   Provides an asynchronous process End method when the process ends.
		/// </summary>
		/// <param name = "result">An <see cref = "T:System.IAsyncResult" /> that contains information about the status of the process. </param>
		public void EndProcessRequest(IAsyncResult result)
		{
			if (result == null)
				return;
			((Task)result).Dispose();
		}

		#endregion
	}
}