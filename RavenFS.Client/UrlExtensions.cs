using System;

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
