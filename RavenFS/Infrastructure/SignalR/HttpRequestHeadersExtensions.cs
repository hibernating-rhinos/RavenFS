using System.Collections.Generic;
using System.Collections.Specialized;
using System;
using System.Net.Http.Headers;
using SignalR.Hosting;

namespace SignalR.AspNetWebApi
{
    internal static class HttpRequestHeadersExtensions
    {
        public static IRequestCookieCollection ParseCookies(this HttpRequestHeaders headers)
        {
            // TODO: Add support for cookies when Web API does
            return new CookieCollection();
        }

        public static NameValueCollection ParseHeaders(this HttpRequestHeaders headers)
        {
            var headerValues = new NameValueCollection();
            foreach (var header in headers)
            {
                foreach (var value in header.Value)
                {
                    headerValues.Add(header.Key, value);
                }
            }
            return headerValues;
        }
    }
}
