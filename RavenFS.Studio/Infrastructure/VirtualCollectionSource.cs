using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public abstract class VirtualCollectionSource<T> : IVirtualCollectionSource<T>
    {
        public event EventHandler<VirtualCollectionChangedEventArgs> CollectionChanged;
        public event EventHandler<DataFetchErrorEventArgs> DataFetchError;

        public abstract int Count { get; }

        public abstract Task<IList<T>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions);

        public virtual void Refresh()
        {
            
        }

        protected void OnCollectionChanged(VirtualCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }

        protected void OnDataFetchError(DataFetchErrorEventArgs e)
        {
            var handler = DataFetchError;
            if (handler != null) handler(this, e);
        }
    }
}