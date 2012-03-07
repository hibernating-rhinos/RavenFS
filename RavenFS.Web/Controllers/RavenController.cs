using System.Collections.Specialized;
using System.Web;
using System.Web.Http;
using RavenFS.Rdc.Wrapper;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Web.Controllers
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


		public BufferPool BufferPool
		{
			get { return RavenFileSystem.Instance.BufferPool; }
		}

		public ISignatureRepository SignatureRepository
		{
			get { return RavenFileSystem.Instance.SignatureRepository; }
		}

		public SigGenerator SigGenerator
		{
			get { return RavenFileSystem.Instance.SigGenerator; }
		}

		private NameValueCollection QueryString
		{
			get { return queryString ?? (queryString = HttpUtility.ParseQueryString(Request.RequestUri.Query)); }
		}

		protected TransactionalStorage Storage
		{
			get { return RavenFileSystem.Instance.Storage; }
		}

		protected IndexStorage Search
		{
			get { return RavenFileSystem.Instance.Search; }
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

				if (pageSize <= 0 || pageSize >= 256)
					pageSize = 256;

				paging = new PagingInfo
				{
					PageSize = pageSize,
					Start = start
				};
				return paging;
			}
		}

	}
}