using System.Web;
using System.Web.Routing;

namespace RavenFS.Infrastructure
{
	public class RedirectRouteHandler : IRouteHandler
	{
		private readonly string newUrl;

		public RedirectRouteHandler(string newUrl)
		{
			this.newUrl = newUrl;
		}

		public IHttpHandler GetHttpHandler(RequestContext requestContext)
		{
			return new RedirectHandler(newUrl);
		}
	}
}