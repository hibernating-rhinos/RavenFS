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

namespace RavenFS.Studio.Infrastructure
{
    public interface IVirtualCollectionSource<T>
    {
        event EventHandler<EventArgs> CollectionChanged;
        int Count { get; }
        Task<IList<T>> GetPageAsync(int start, int pageSize);
    }
}
