using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public class LazyLoadingCollection<T> : IList<VirtualItem<T>>, INotifyCollectionChanged, INotifyPropertyChanged where T:class
    {
        private readonly int _pageSize;
        private const int MaximumNumberOfIndividualCollectionChangedEventsToRaise = 10;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        private Func<Task<int>> _rowCountFetcher;
        private Func<int, int, Task<IList<T>>> _pageFetcher;

        private List<VirtualItem<T>> _virtualItems = new List<VirtualItem<T>>();
        private HashSet<int> _fetchedPages = new HashSet<int>();
        private HashSet<int> _requestedPages = new HashSet<int>();
        private LruList<int> _mostRecentlyRequestedPages = new LruList<int>(1);
        private int _windowSize;

        private SynchronizationContext _synchronizationContext;
        private bool _isRefreshInProgress;

        public LazyLoadingCollection(int pageSize)
        {
            _pageSize = pageSize;
            _synchronizationContext = SynchronizationContext.Current;
            WindowSize = 50;
        }

        public Func<Task<int>> RowCountFetcher
        {
            get { return _rowCountFetcher; }
            set { 
                _rowCountFetcher = value;
                Refresh();
            }
        }

        public Func<int, int, Task<IList<T>>> PageFetcher
        {
            get { return _pageFetcher; }
            set { _pageFetcher = value;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_isRefreshInProgress)
            {
                return;
            }

            _isRefreshInProgress = true;

            MarkExistingItemsAsStale();

            _fetchedPages.Clear();
            _requestedPages.Clear();

            BeginUpdateItemCount();
        }

        private void MarkExistingItemsAsStale()
        {
            foreach (var page in _fetchedPages)
            {
                var startIndex = page*_pageSize;
                var endIndex = Math.Min((page + 1)*_pageSize, _virtualItems.Count);

                for (int i = startIndex; i < endIndex; i++)
                {
                    _virtualItems[i].IsStale = true;
                }
            }
        }

        private void BeginUpdateItemCount()
        {
            _rowCountFetcher()
                .ContinueWith(
                    t => _synchronizationContext.Post(newItemCount => UpdateItemCount((int)newItemCount), t.Result),
                    TaskContinuationOptions.ExecuteSynchronously);
        }

        private void UpdateItemCount(int newItemCount)
        {
            var delta = newItemCount - _virtualItems.Count;

            if (delta > 0)
            {
                var newItems = GenerateNewVirtualItems(delta, _virtualItems.Count);
                _virtualItems.AddRange(newItems);

                RaiseCollectionChangedEventsForNewItems(newItems, _virtualItems.Count - delta);
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            }
            else if (delta < 0)
            {
                var numberToRemove = -delta;
                var startingIndex = _virtualItems.Count - numberToRemove;

                var removedItems = numberToRemove < MaximumNumberOfIndividualCollectionChangedEventsToRaise
                                       ? _virtualItems.GetRange(startingIndex, numberToRemove)
                                       : null;

                _virtualItems.RemoveRange(startingIndex, numberToRemove);

                RaiseCollectionChangedEventsForRemovedItems(removedItems, startingIndex);
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            }

            _isRefreshInProgress = false;

            BeginRefreshVisibleItems();
        }

        private void BeginRefreshVisibleItems()
        {
            foreach (var page in _mostRecentlyRequestedPages)
            {
                BeginGetPage(page);
            }
        }

        private void RaiseCollectionChangedEventsForRemovedItems(IList<VirtualItem<T>> removedItems, int startingIndex)
        {
            if (removedItems != null)
            {
                // work backwards when remove items, otherwise indexes won't make sense to recipients
                for (int i = removedItems.Count - 1; i >= 0; i--)
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems[i], startingIndex + i));
                }
            }
            else
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private void RaiseCollectionChangedEventsForNewItems(IList<VirtualItem<T>> newItems, int startingIndex)
        {
            if (newItems.Count > MaximumNumberOfIndividualCollectionChangedEventsToRaise)
            {
                // for lots of new items, signalling a reset is probably more efficient than signalling each new addition
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            else
            {
                for (int i = 0; i < newItems.Count; i++)
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems[i], startingIndex + i));
                }
            }
        }

        private IList<VirtualItem<T>> GenerateNewVirtualItems(int count, int startingIndex)
        {
            var list = new List<VirtualItem<T>>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new VirtualItem<T>(this, startingIndex + i));
            }

            return list;
        }

        private void BeginGetPage(int page)
        {

            if (_isRefreshInProgress || IsPageAlreadyRequested(page))
            {
                return;
            }

            _requestedPages.Add(page);

            _pageFetcher(page*_pageSize, _pageSize).ContinueWith(
                t => _synchronizationContext.Post(_ => UpdatePage(page, t.Result), null),
                TaskContinuationOptions.ExecuteSynchronously);
        }

        private bool IsPageAlreadyRequested(int page)
        {
            return _fetchedPages.Contains(page) || _requestedPages.Contains(page);
        }

        private void UpdatePage(int page, IList<T> results)
        {
            _requestedPages.Remove(page);
            _fetchedPages.Add(page);

            var startIndex = page*_pageSize;
            
            if (startIndex >= _virtualItems.Count)
            {
                return;
            }

            var numberThatWillFit = Math.Min(_virtualItems.Count - startIndex, results.Count);

            for (int i = 0; i < numberThatWillFit; i++)
            {
                _virtualItems[startIndex + i].Item = results[i];
            }
        }

        public void RealizeItemRequested(int index)
        {
            var page = index/_pageSize;
            BeginGetPage(page);
        }

        public IEnumerator<VirtualItem<T>> GetEnumerator()
        {
            return _virtualItems.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(VirtualItem<T> item)
        {
            return _virtualItems.Contains(item);
        }

        public void CopyTo(VirtualItem<T>[] array, int arrayIndex)
        {
            _virtualItems.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _virtualItems.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        protected int TotalPages
        {
            get { return (int)Math.Ceiling((double) Count/_pageSize); }
        }


        public int WindowSize
        {
            get { return _windowSize; }
            set
            {
                _windowSize = value;
                _mostRecentlyRequestedPages.Size = GetWindowSizeInPages();
            }
        }

        private int GetWindowSizeInPages()
        {
            return _windowSize/_pageSize + 1;
        }

        #region Not Implemented IList methods

        public int IndexOf(VirtualItem<T> item)
        {
            return _virtualItems.IndexOf(item);
        }

        public VirtualItem<T> this[int index]
        {
            get { return _virtualItems[index]; }
            set { throw new NotImplementedException(); }
        }

        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            NotifyCollectionChangedEventHandler handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, e);
        }

        public void Add(VirtualItem<T> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Remove(VirtualItem<T> item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, VirtualItem<T> item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
