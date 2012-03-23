using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public abstract class VirtualCollectionSource<T> : IVirtualCollectionSource<T>
    {
        public event EventHandler<VirtualCollectionChangedEventArgs> CollectionChanged;

        public abstract int Count { get; }

        public abstract Task<IList<T>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions);

        protected void OnCollectionChanged(VirtualCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }
    }
}