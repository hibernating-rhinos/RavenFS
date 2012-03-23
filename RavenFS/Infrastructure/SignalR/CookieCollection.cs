using System;
using SignalR.Hosting;

namespace SignalR.AspNetWebApi
{
    internal class CookieCollection : IRequestCookieCollection
    {
        public Cookie this[string name]
        {
            get { throw new NotImplementedException(); }
        }

        public int Count
        {
            get { throw new NotImplementedException(); }
        }
    }
}
