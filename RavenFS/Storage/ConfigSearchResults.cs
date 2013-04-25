using System.Collections.Generic;

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