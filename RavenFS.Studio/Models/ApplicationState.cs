using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
