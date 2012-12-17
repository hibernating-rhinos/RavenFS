using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Util
{
    public static class FilePathTools
    {
        public static string Cannoicalise(string filePath)
        {
            if (!filePath.StartsWith("/"))
            {
                filePath = "/" + filePath;
            }

            return filePath;
        }
    }
}