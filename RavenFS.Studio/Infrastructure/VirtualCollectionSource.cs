using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public abstract class VirtualCollectionSource<T> : IVirtualCollectionSource<T>
    {
        public event EventHandler<EventArgs> SourceChanged;

        public abstract int? Count { get; }

        public abstract Task<IList<T>> GetPageAsync(int start, int pageSize);

        protected void OnSourceChanged(EventArgs e)
        {
            EventHandler<EventArgs> handler = SourceChanged;
            if (handler != null) handler(this, e);
        }
    }
}