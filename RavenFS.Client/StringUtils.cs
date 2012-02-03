using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if SILVERLIGHT
using System.Net.Browser;
#else
using System.Web;
#endif


namespace RavenFS.Client
{
    public static class StringUtils
    {
        public static string UrlEncode(string textToEncode)
        {
#if SILVERLIGHT
            return Uri.EscapeUriString(textToEncode)
#else
            return HttpUtility.UrlEncode(textToEncode);
#endif
        }
    }
}
