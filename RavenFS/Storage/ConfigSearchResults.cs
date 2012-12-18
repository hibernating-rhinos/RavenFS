using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Storage
{
    public class ConfigSearchResults
    {
        public IList<string> ConfigNames { get; set; }
        public int TotalCount { get; set; }
        public int Start { get; set; }
        public int PageSize { get; set; }
    }
}