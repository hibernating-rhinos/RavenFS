using System.Collections.Generic;
using RavenFS.Client;

namespace RavenFS.Studio.Models
{
    public class ApplicationState
    {
        public ApplicationState()
        {
            VirtualFolders = new VirtualFoldersManager();
            ModifiedConfigurations = new Dictionary<string, NameValueCollection>();
        }

        public VirtualFoldersManager VirtualFolders { get; private set; }

        public IDictionary<string, NameValueCollection> ModifiedConfigurations { get; private set; }

        public string LastSearch { get; set; }

        public IList<string> CachedSearchTerms { get; set; }
    }
}
