using System;
using System.Collections;

namespace RavenFS.Studio.Infrastructure
{
    public class ItemSelection
    {
        private int count;
        private Func<IEnumerable> selectedItemsProvider;

        public event EventHandler<EventArgs> SelectionChanged;

        public event EventHandler<DesiredSelectionChangedEventArgs> DesiredSelectionChanged;

        public void NotifySelectionChanged(int count, Func<IEnumerable> selectedItemsEnumerableProvider)
        {
            this.count = count;
            selectedItemsProvider = selectedItemsEnumerableProvider;
            OnSelectionChanged(EventArgs.Empty);
        }

        int Count { get { return count; } }

        protected IEnumerable GetSelectedItems()
        {
	        if (selectedItemsProvider != null)
            {
                var snapshot = selectedItemsProvider();
                return snapshot;
            }
	        
			return new object[0];
        }

	    protected void SetDesiredSelection(IList items)
        {
            OnDesiredSelectionChanged(new DesiredSelectionChangedEventArgs(items));
        }

        protected virtual void OnDesiredSelectionChanged(DesiredSelectionChangedEventArgs e)
        {
            var handler = DesiredSelectionChanged;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnSelectionChanged(EventArgs e)
        {
            var handler = SelectionChanged;
            if (handler != null) handler(this, e);
        }
    }

    public class DesiredSelectionChangedEventArgs : EventArgs
    {
        private readonly IList items;

        public DesiredSelectionChangedEventArgs(IList items)
        {
            this.items = items;
        }

        public IList Items
        {
            get { return items; }
        }
    }
}
