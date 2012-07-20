using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenFS.Client
{
    public static class UrlExtensions
    {
        public static string NoCache(this string url)
        {
            return (url.Contains("?"))
                ? url + "&noCache=" + Guid.NewGuid().GetHashCode()
                : url + "?noCache=" + Guid.NewGuid().GetHashCode();
        }
    }
}
