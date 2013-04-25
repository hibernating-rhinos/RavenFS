using System.Collections.Generic;

namespace RavenFS.Studio.Infrastructure
{
    public abstract class PageModel : ViewModel
    {
        public IDictionary<string, string> QueryParameters { get; set; }
    }
}
