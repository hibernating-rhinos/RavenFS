using System;
using System.Collections.Specialized;
using System.Net.Http;
using SignalR.Hosting;

namespace SignalR.AspNetWebApi
{
    internal class WebApiRequest : IRequest
    {
        private readonly HttpRequestMessage _httpRequestMessage;
        private readonly Lazy<IRequestCookieCollection> _cookies;
        private readonly Lazy<NameValueCollection> _form;
        private readonly Lazy<NameValueCollection> _headers;
        private readonly Lazy<NameValueCollection> _queryString;

        public WebApiRequest(HttpRequestMessage httpRequestMessage)
        {
            _httpRequestMessage = httpRequestMessage;

            _cookies = new Lazy<IRequestCookieCollection>(() => _httpRequestMessage.Headers.ParseCookies());

            _form = new Lazy<NameValueCollection>(() => _httpRequestMessage.Content.ReadAsNameValueCollection());

            _headers = new Lazy<NameValueCollection>(() => _httpRequestMessage.Headers.ParseHeaders());

            _queryString = new Lazy<NameValueCollection>(() => Url.ParseQueryString());
        }

        public IRequestCookieCollection Cookies
        {
            get
            {
                return _cookies.Value;
            }
        }

        public NameValueCollection Form
        {
            get
            {
                return _form.Value;
            }
        }

        public NameValueCollection Headers
        {
            get
            {
                return _headers.Value;
            }
        }

        public NameValueCollection QueryString
        {
            get
            {
                return _queryString.Value;
            }
        }

        public Uri Url
        {
            get
            {
                return _httpRequestMessage.RequestUri;
            }
        }
    }
}
