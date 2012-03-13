using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class FilesCollectionSource : VirtualCollectionSource<FileInfo>
    {
        public override Task<int> GetItemCountAsync()
        {
            return ApplicationModel.Current.Client.StatsAsync().ContinueOnSuccess(t => (int) t.FileCount);
        }

        public override Task<IList<FileInfo>> GetPageAsync(int start, int pageSize)
        {
            return ApplicationModel.Current.Client.BrowseAsync(start, pageSize)
                .ContinueOnSuccess(t => (IList<FileInfo>)t);
        }

        public void Refresh()
        {
            OnCollectionChanged(EventArgs.Empty);
        }
    }
}
