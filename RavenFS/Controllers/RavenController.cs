using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Wrapper;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	public abstract class RavenController : ApiController
	{
		protected class PagingInfo
		{
			public int Start;
			public int PageSize;
		}

		NameValueCollection queryString;
		private PagingInfo paging;

		private RavenFileSystem ravenFileSystem;

		public RavenFileSystem  RavenFileSystem
		{
			get
			{
				if (ravenFileSystem == null)
					ravenFileSystem = (RavenFileSystem) ControllerContext.Configuration.ServiceResolver.GetService(typeof (RavenFileSystem));
				return ravenFileSystem;
			}
		}

		protected Task<T> Result<T>(T result)
		{
			var tcs = new TaskCompletionSource<T>();
			tcs.SetResult(result);
			return tcs.Task;
		}

		public BufferPool BufferPool
		{
			get { return RavenFileSystem.BufferPool; }
		}

		public ISignatureRepository SignatureRepository
		{
			get { return RavenFileSystem.SignatureRepository; }
		}

		public SigGenerator SigGenerator
		{
			get { return RavenFileSystem.SigGenerator; }
		}

		private NameValueCollection QueryString
		{
			get { return queryString ?? (queryString = HttpUtility.ParseQueryString(Request.RequestUri.Query)); }
		}

		protected TransactionalStorage Storage
		{
			get { return RavenFileSystem.Storage; }
		}

		protected IndexStorage Search
		{
			get { return RavenFileSystem.Search; }
		}

		protected PagingInfo Paging
		{
			get
			{
				if (paging != null)
					return paging;

				int start;
				int.TryParse(QueryString["start"], out start);

				int pageSize;
				int.TryParse(QueryString["pageSize"], out pageSize);

				paging = new PagingInfo
				{
					PageSize = Math.Max(1024, Math.Min(25, pageSize)),
					Start = start
				};
				return paging;
			}
		}

		protected HttpResponseMessage StreamResult(string filename, Stream resultContent)
		{
			var response = new HttpResponseMessage
			{
				Headers =
				{
					TransferEncodingChunked = false
				}
			};
			long length = 0;
			ContentRangeHeaderValue contentRange = null;
			if (Request.Headers.Range != null)
			{
				if (Request.Headers.Range.Ranges.Count != 1)
				{
					throw new InvalidOperationException("Can't handle multiple range values");
				}
				var range = Request.Headers.Range.Ranges.First();
				var from = range.From ?? 0;
				var to = range.To ?? resultContent.Length;

				length = (to - from);

				contentRange = new ContentRangeHeaderValue(from, to, resultContent.Length);
				resultContent = new LimitedStream(resultContent, from, to);
			}
			else
			{
				length = resultContent.Length;
			}

			response.Content = new StreamContent(resultContent)
			{
				Headers =
				{
					ContentDisposition = new ContentDispositionHeaderValue("attachment")
					{
						FileName = filename
					},
					ContentLength = length,
					ContentRange = contentRange,
				}
			};

			return response;
		}

	}
}