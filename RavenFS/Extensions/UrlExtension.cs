//-----------------------------------------------------------------------
// <copyright file="UrlExtension.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Web;

namespace RavenFS.Extensions
{
	public static class UrlExtension
	{
		public static string GetRequestUrl(this HttpContext context)
		{
			string localPath = context.Request.Url.LocalPath;
		    var virtualPath = HttpRuntime.AppDomainAppVirtualPath;

		    if (!string.IsNullOrEmpty(virtualPath) && virtualPath != "/" &&
                localPath.StartsWith(virtualPath, StringComparison.InvariantCultureIgnoreCase))
			{
                localPath = localPath.Substring(virtualPath.Length);
				if (localPath.Length == 0)
					localPath = "/";
			}
			return localPath;
		}
	}
}