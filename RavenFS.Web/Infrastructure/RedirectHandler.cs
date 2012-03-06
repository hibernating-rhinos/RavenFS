using System.Web;

namespace RavenFS.Web.Infrastructure
{
	public class RedirectHandler : IHttpHandler
	{
		private readonly string newUrl;

		public RedirectHandler(string newUrl)
		{
			this.newUrl = newUrl;
		}

		public bool IsReusable
		{
			get { return true; }
		}

		public void ProcessRequest(HttpContext httpContext)
		{
			httpContext.Response.Status = "301 Moved Permanently";
			httpContext.Response.StatusCode = 301;
			httpContext.Response.AppendHeader("Location", newUrl);
			return;
		}
	}
}