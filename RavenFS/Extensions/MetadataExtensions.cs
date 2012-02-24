//-----------------------------------------------------------------------
// <copyright file="MetadataExtensions.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using System;
using RavenFS.Storage;

namespace Raven.Abstractions.Extensions
{
    /// <summary>
    /// Extensions for handling metadata
    /// </summary>
    public static class MetadataExtensions
    {
		public static void AddHeaders(HttpContext context, FileAndPages fileAndPages)
		{
			foreach (var key in fileAndPages.Metadata.AllKeys)
			{
				var values = fileAndPages.Metadata.GetValues(key);
				if (values == null)
					continue;

				foreach (var value in values)
				{
					context.Response.AddHeader(key, value);

				}
			}
		}        

        private static readonly HashSet<string> HeadersToIgnoreClient = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			// Raven internal headers
			"Raven-Server-Build",
			"Non-Authoritive-Information",
			"Raven-Timer-Request",

            //proxy
            "Reverse-Via",

            "Allow",
            "Content-Disposition",
            "Content-Encoding",
            "Content-Language",
            "Content-Location",
            "Content-MD5",
            "Content-Range",
            "Content-Type",
            "Expires",
			// ignoring this header, we handle this internally
			"Last-Modified",
			// Ignoring this header, since it may
			// very well change due to things like encoding,
			// adding metadata, etc
			"Content-Length",
			// Special things to ignore
			"Keep-Alive",
			"X-Powered-By",
			"X-AspNet-Version",
			"X-Requested-With",
			"X-SourceFiles",
			// Request headers
			"Accept-Charset",
			"Accept-Encoding",
			"Accept",
			"Accept-Language",
			"Authorization",
			"Cookie",
			"Expect",
			"From",
			"Host",
			"If-Match",
			"If-Modified-Since",
			"If-None-Match",
			"If-Range",
			"If-Unmodified-Since",
			"Max-Forwards",
			"Referer",
			"TE",
			"User-Agent",
			//Response headers
			"Accept-Ranges",
			"Age",
			"Allow",
			"ETag",
			"Location",
			"Retry-After",
			"Server",
			"Set-Cookie2",
			"Set-Cookie",
			"Vary",
			"Www-Authenticate",
			// General
			"Cache-Control",
			"Connection",
			"Date",
			"Pragma",
			"Trailer",
			"Transfer-Encoding",
			"Upgrade",
			"Via",
			"Warning",
		};

        /// <summary>
        /// Filters the headers from unwanted headers
        /// </summary>
        /// <param name="self">The self.</param>
        /// <returns></returns>public static RavenJObject FilterHeaders(this System.Collections.Specialized.NameValueCollection self, bool isServerDocument)
        public static NameValueCollection FilterHeaders(this NameValueCollection self)
        {
            var metadata = new NameValueCollection();
            foreach (string header in self)
            {
            	if (header.StartsWith("Temp"))
            		continue;
            	if (HeadersToIgnoreClient.Contains(header))
            		continue;
            	var values = self.GetValues(header);
            	var headerName = CaptureHeaderName(header);

            	if (values == null)
            		continue;

            	foreach (var value in values)
            	{
            		metadata.Add(headerName, value);
            	}
            }
        	return metadata;
        }

        public static NameValueCollection UpdateLastModified(this NameValueCollection self)
        {
            self["Last-Modified"] = DateTime.UtcNow.ToString("O");
            return self;
        }

        private static string CaptureHeaderName(string header)
        {
            var lastWasDash = true;
            var sb = new StringBuilder(header.Length);

            foreach (var ch in header)
            {
                sb.Append(lastWasDash ? char.ToUpper(ch) : ch);

                lastWasDash = ch == '-';
            }

            return sb.ToString();
        }
    }
}
