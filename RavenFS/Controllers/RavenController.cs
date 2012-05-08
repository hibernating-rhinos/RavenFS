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
using RavenFS.Notifications;
using RavenFS.Rdc.Wrapper;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using System.Net;
	using Rdc;
	using Rdc.Conflictuality;

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

	    public NotificationPublisher Publisher
	    {
	        get { return ravenFileSystem.Publisher; }
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

		public SigGenerator SigGenerator
		{
			get { return RavenFileSystem.SigGenerator; }
		}

	    public HistoryUpdater HistoryUpdater
	    {
            get { return RavenFileSystem.HistoryUpdater;  }
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

		protected FileLockManager FileLockManager
		{
			get { return RavenFileSystem.FileLockManager; }
		}

		protected ConflictActifactManager ConflictActifactManager
		{
			get { return RavenFileSystem.ConflictActifactManager; }
		}

		protected ConflictDetector ConflictDetector
		{
			get { return RavenFileSystem.ConflictDetector; }
		}

		protected ConflictResolver ConflictResolver
		{
			get { return RavenFileSystem.ConflictResolver; }
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
				if (int.TryParse(QueryString["pageSize"], out pageSize) == false)
					pageSize = 25;

				paging = new PagingInfo
				{
					PageSize = Math.Min(1024, Math.Max(1, pageSize)),
					Start = Math.Max(start, 0)
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

                // "to" in Content-Range points on the last byte. In other words the set is: <from..to>  not <from..to)
                if (from < to)
                {
                    contentRange = new ContentRangeHeaderValue(from, to - 1, resultContent.Length);
                    resultContent = new LimitedStream(resultContent, from, to);
                }
                else
                {
                    contentRange = new ContentRangeHeaderValue(0);
                    resultContent = Stream.Null;
                }
			    
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

		protected void AssertFileIsNotBeingSynced(string fileName, StorageActionsAccessor accessor)
		{
			if (FileLockManager.TimeoutExceeded(fileName, accessor))
			{
				FileLockManager.UnlockByDeletingSyncConfiguration(fileName, accessor);
			}
			else
			{
				throw new HttpResponseException(string.Format("File {0} is being synced", fileName),
				                                HttpStatusCode.PreconditionFailed);
			}
		}
	}
}