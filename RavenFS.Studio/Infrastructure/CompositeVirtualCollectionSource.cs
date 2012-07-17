using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    public class CompositeVirtualCollectionSource<T> : IVirtualCollectionSource<T>, INotifyBusyness
    {
        public event EventHandler<VirtualCollectionSourceChangedEventArgs> CollectionChanged;
        public event EventHandler<EventArgs> IsBusyChanged;

        private readonly IVirtualCollectionSource<T> source1;
        private readonly IVirtualCollectionSource<T> source2;

        public CompositeVirtualCollectionSource(IVirtualCollectionSource<T> source1, IVirtualCollectionSource<T> source2)
        {
            this.source1 = source1;
            this.source2 = source2;

            source1.CollectionChanged += HandleChildCollectionChanged;
            source2.CollectionChanged += HandleChildCollectionChanged;

            if (source1 is INotifyBusyness)
            {
                ((INotifyBusyness) source1).IsBusyChanged += HandleIsBusyChanged;
            }

            if (source2 is INotifyBusyness)
            {
                ((INotifyBusyness)source2).IsBusyChanged += HandleIsBusyChanged;
            }
        }

        private void HandleIsBusyChanged(object sender, EventArgs e)
        {
            OnIsBusyChanged(e);
        }

        private void HandleChildCollectionChanged(object sender, VirtualCollectionSourceChangedEventArgs e)
        {
            OnCollectionChanged(e);
        }

        protected void OnCollectionChanged(VirtualCollectionSourceChangedEventArgs e)
        {
            EventHandler<VirtualCollectionSourceChangedEventArgs> handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }

        protected void OnIsBusyChanged(EventArgs e)
        {
            EventHandler<EventArgs> handler = IsBusyChanged;
            if (handler != null) handler(this, e);
        }

        public int Count
        {
            get { return Source1.Count + Source2.Count; }
        }

        protected IVirtualCollectionSource<T> Source1
        {
            get { return source1; }
        }

        protected IVirtualCollectionSource<T> Source2
        {
            get { return source2; }
        }

        public Task<IList<T>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            var source1Count = Source1.Count;

            if (start < source1Count - pageSize)
            {
                return Source1.GetPageAsync(start, pageSize, sortDescriptions);
            }
            else if (start > source1Count)
            {
                var source2Start = start - source1Count;
                return Source2.GetPageAsync(source2Start, pageSize, sortDescriptions);
            }
            else
            {
                var source1PageSize = source1Count - start;
                var source2PageSize = pageSize - source1PageSize;

                // we need to mash up the two sources to provide a full page
                var source1Results = Source1.GetPageAsync(start, source1PageSize, sortDescriptions);
                var source2Results = Source2.GetPageAsync(0, source2PageSize, sortDescriptions);

                var result =
                    TaskEx.WhenAll(source1Results, source2Results)
                        .ContinueWith(_ => (IList<T>)source1Results.Result.Concat(source2Results.Result).ToArray());

                return result;
            }
        }

        public void Refresh(RefreshMode mode)
        {
            Source1.Refresh(mode);
            Source2.Refresh(mode);
        }

        public bool IsBusy
        {
            get
            {
                return (Source1 is INotifyBusyness && ((INotifyBusyness) Source1).IsBusy)
                       || (Source2 is INotifyBusyness && ((INotifyBusyness) Source2).IsBusy);
            }
        }
    }
}
