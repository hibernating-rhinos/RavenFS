using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace RavenFS.Controllers
{
	public class StaticController : ApiController
	{
		public HttpResponseMessage ClientAccessPolicy()
		{
			const string access =
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
</access-policy>";
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(access)
				{
					Headers =
						{
							ContentType = new MediaTypeHeaderValue("text/xml")
						}
				}
			};
		}
	}
}