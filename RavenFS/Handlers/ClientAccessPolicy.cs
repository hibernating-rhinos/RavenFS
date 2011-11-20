using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/ClientAccessPolicy.xml$", "GET")]
	public class ClientAccessPolicy : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			context.Response.AddHeader("ETag", typeof(ClientAccessPolicy).FullName);
			context.Response.Cache.SetCacheability(HttpCacheability.Public);
			context.Response.ContentType = "text/xml";
		
			return context.Response.Output.WriteAsync(
					@"<?xml version='1.0' encoding='utf-8'?>
<access-policy>
	<cross-domain-access>
		<policy>
			<allow-from http-methods='*' http-request-headers='*'>
				<domain uri='*' />
			</allow-from>
			<grant-to>
				<resource include-subpaths='true' path='/' />
			</grant-to>
		</policy>
	</cross-domain-access>
</access-policy>");
		}
	}
}