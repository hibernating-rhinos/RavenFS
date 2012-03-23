using System.Collections.Specialized;
using System.Net.Http;

namespace SignalR.AspNetWebApi
{
    internal static class HttpContentExtensions
    {
        public static NameValueCollection ReadAsNameValueCollection(this HttpContent content)
        {
            var form = new NameValueCollection();

            // TODO: Do a safe synchronous read of the request message content to form values


            return form;
        }
    }
}
