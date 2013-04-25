using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public class CompositeVirtualCollectionSource<T> : IVirtualCollectionSource<T>, INotifyBusyness
    {
        public event EventHandler<VirtualCollectionSourceChangedEventArgs> CollectionChanged;
        public event EventHandler<EventArgs> CountChanged;
        public event EventHandler<EventArgs> IsBusyChanged;

        private readonly IVirtualCollectionSource<T> source1;
        private readonly IVirtualCollectionSource<T> source2;

        public CompositeVirtualCollectionSource(IVirtualCollectionSource<T> source1, IVirtualCollectionSource<T> source2)
        {
            this.source1 = source1;
            this.source2 = source2;

            source1.CollectionChanged += HandleChildCollectionChanged;
            source2.CollectionChanged += HandleChildCollectionChanged;

            source1.CountChanged += HandleCountChanged;
            source2.CountChanged += HandleCountChanged;

	        var notifyBusyness = source1 as INotifyBusyness;
	        if (notifyBusyness != null)
		        notifyBusyness.IsBusyChanged += HandleIsBusyChanged;

	        var busyness = source2 as INotifyBusyness;
	        if (busyness != null)
		        busyness.IsBusyChanged += HandleIsBusyChanged;
        }

        private void HandleCountChanged(object sender, EventArgs e)
        {
            OnCountChanged(EventArgs.Empty);
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
            var handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }

        protected void OnCountChanged(EventArgs e)
        {
            var handler = CountChanged;
            if (handler != null) handler(this, e);
        }

        protected void OnIsBusyChanged(EventArgs e)
        {
            var handler = IsBusyChanged;
            if (handler != null) handler(this, e);
        }

        public int? Count
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

        public async Task<IList<T>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            await EnsureChildCollectionCountsInitialised();

            var source1Count = Source1.Count;
            while (!source1Count.HasValue)
            {
                await EnsureChildCollectionCountsInitialised();
                source1Count = Source1.Count;
            }

	        if (start < source1Count.Value - pageSize)
		        return await Source1.GetPageAsync(start, pageSize, sortDescriptions);
	        
			if (start > source1Count)
	        {
		        var source2Start = start - source1Count.Value;
		        return await Source2.GetPageAsync(source2Start, pageSize, sortDescriptions);
	        }
	        
			var source1PageSize = source1Count.Value - start;
	        var source2PageSize = pageSize - source1PageSize;

	        // we need to mash up the two sources to provide a full page
	        var source1Results = Source1.GetPageAsync(start, source1PageSize, sortDescriptions);
	        var source2Results = Source2.GetPageAsync(0, source2PageSize, sortDescriptions);

	        await TaskEx.WhenAll(source1Results, source2Results);
	        return (IList<T>)source1Results.Result.Concat(source2Results.Result).ToArray();
        }

        private async Task EnsureChildCollectionCountsInitialised()
        {
            var forceInitialiseCountTasks = new List<Task>();

	        if (!source1.Count.HasValue)
		        forceInitialiseCountTasks.Add(Source1.GetPageAsync(0, 0, null));

	        if (!source2.Count.HasValue)
		        forceInitialiseCountTasks.Add(Source2.GetPageAsync(0, 0, null));

	        if (forceInitialiseCountTasks.Count > 0)
		        await TaskEx.WhenAll(forceInitialiseCountTasks);
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
