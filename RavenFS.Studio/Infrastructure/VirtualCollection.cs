using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    /// <summary>
    /// Implements a collection that loads its items by pages only when requested
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>The trick to ensuring that the silverlight datagrid doesn't attempt to enumerate all
    /// items from its DataSource in one shot is to implement both IList and ICollectionView.</remarks>
    public class VirtualCollection<T> : IList<VirtualItem<T>>, IList, ICollectionView, INotifyPropertyChanged where T : class
    {
        private readonly int _pageSize;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        private Func<Task<int>> _rowCountFetcher;
        private Func<int, int, Task<IList<T>>> _pageFetcher;

        private readonly SparseList<VirtualItem<T>> _virtualItems;
        private readonly HashSet<int> _fetchedPages = new HashSet<int>();
        private readonly HashSet<int> _requestedPages = new HashSet<int>();
        private int _itemCount;
        private readonly TaskScheduler _synchronizationContextScheduler;
        private bool _isRefreshInProgress;
        private int _currentItem;

        public VirtualCollection(int pageSize)
        {
            if (pageSize < 1)
            {
                throw new ArgumentException("pageSize must be bigger than 0");
            }

            _pageSize = pageSize;
            _virtualItems = new SparseList<VirtualItem<T>>(DetermineSparseListPageSize(pageSize));
            _currentItem = -1;
            _synchronizationContextScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        private int DetermineSparseListPageSize(int fetchPageSize)
        {
            // we don't want the sparse list to have pages that are too small,
            // because that will harm performance by fragmenting the list across memory,
            // but too big, and we'll be wasting lots of space
            const int TargetSparseListPageSize = 100;

            if (fetchPageSize > TargetSparseListPageSize)
            {
                return fetchPageSize;
            }
            else
            {
                // return the smallest multiple of fetchPageSize that is bigger than TargetSparseListPageSize
                return (int)Math.Ceiling((double)TargetSparseListPageSize / fetchPageSize) * fetchPageSize;
            }
        }

        public Func<Task<int>> RowCountFetcher
        {
            get { return _rowCountFetcher; }
            set
            {
                _rowCountFetcher = value;
                Refresh();
            }
        }

        public Func<int, int, Task<IList<T>>> PageFetcher
        {
            get { return _pageFetcher; }
            set
            {
                _pageFetcher = value;
                Refresh();
            }
        }

        private void UpdateItemCount(int newItemCount)
        {
            _isRefreshInProgress = false;

            var wasCurrentBeyondLast = IsCurrentAfterLast;

            _itemCount = newItemCount;

            if (IsCurrentAfterLast && !wasCurrentBeyondLast)
            {
                UpdateCurrentPosition(_itemCount - 1, allowCancel: false);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void MarkExistingItemsAsStale()
        {
            foreach (var page in _fetchedPages)
            {
                var startIndex = page * _pageSize;
                var endIndex = (page + 1) * _pageSize;

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (_virtualItems[i] != null)
                    {
                        _virtualItems[i].IsStale = true;
                    }
                }
            }
        }

        private void BeginUpdateItemCount()
        {
            _rowCountFetcher()
                .ContinueWith(
                    t => UpdateItemCount(t.Result),
                    _synchronizationContextScheduler);
        }

        private void BeginGetPage(int page)
        {
            if (_isRefreshInProgress || IsPageAlreadyRequested(page))
            {
                return;
            }

            _requestedPages.Add(page);

            _pageFetcher(page*_pageSize, _pageSize).ContinueWith(
                t => UpdatePage(page, t.Result),
                _synchronizationContextScheduler);
        }

        private bool IsPageAlreadyRequested(int page)
        {
            return _fetchedPages.Contains(page) || _requestedPages.Contains(page);
        }

        private void UpdatePage(int page, IList<T> results)
        {
            _requestedPages.Remove(page);
            _fetchedPages.Add(page);

            var startIndex = page * _pageSize;

            for (int i = 0; i < results.Count; i++)
            {
                var index = startIndex + i;
                var virtualItem = _virtualItems[index] ?? (_virtualItems[index] = new VirtualItem<T>(this, index));
                virtualItem.Item = results[i];
            }
        }

        public void RealizeItemRequested(int index)
        {
            var page = index / _pageSize;
            BeginGetPage(page);
        }

        public bool Contains(object item)
        {
            if (item is VirtualItem<T>)
            {
                return Contains(item as VirtualItem<T>);
            }
            else
            {
                return false;
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

        public int IndexOf(VirtualItem<T> item)
        {
            return item.Index;
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { throw new NotImplementedException(); }
        }

        public VirtualItem<T> this[int index]
        {
            get
            {
                RealizeItemRequested(index);
                return _virtualItems[index] ?? (_virtualItems[index] = new VirtualItem<T>(this, index));
            }
            set { throw new NotImplementedException(); }
        }

        public IDisposable DeferRefresh()
        {
            throw new NotImplementedException();
        }

        public bool MoveCurrentToFirst()
        {
            return UpdateCurrentPosition(0);
        }

        public bool MoveCurrentToLast()
        {
            return UpdateCurrentPosition(_itemCount - 1);
        }

        public bool MoveCurrentToNext()
        {
            return UpdateCurrentPosition(CurrentPosition + 1);
        }

        public bool MoveCurrentToPrevious()
        {
            return UpdateCurrentPosition(CurrentPosition - 1);
        }

        public bool MoveCurrentTo(object item)
        {
            return MoveCurrentToPosition(((IList)this).IndexOf(item));
        }

        public bool MoveCurrentToPosition(int position)
        {
            return UpdateCurrentPosition(position);
        }

        public CultureInfo Culture { get; set; }

        public IEnumerable SourceCollection
        {
            get { return this; }
        }

        public Predicate<object> Filter
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool CanFilter
        {
            get { return false; }
        }

        public SortDescriptionCollection SortDescriptions
        {
            get { throw new NotImplementedException(); }
        }

        public bool CanSort
        {
            get { return false; }
        }

        public bool CanGroup
        {
            get { return false; }
        }

        public ObservableCollection<GroupDescription> GroupDescriptions
        {
            get { throw new NotImplementedException(); }
        }

        public ReadOnlyObservableCollection<object> Groups
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsEmpty
        {
            get { return _itemCount == 0; }
        }

        public object CurrentItem
        {
            get { return 0 < CurrentPosition && CurrentPosition < _itemCount ? this[CurrentPosition] : null; }
        }

        public int CurrentPosition
        {
            get { return _currentItem; }
            private set
            {
                _currentItem = value;
                OnCurrentChanged(EventArgs.Empty);
            }
        }

        private bool UpdateCurrentPosition(int newCurrentPosition, bool allowCancel = true)
        {
            var changingEventArgs = new CurrentChangingEventArgs(allowCancel);

            OnCurrentChanging(changingEventArgs);

            if (!changingEventArgs.Cancel)
            {
                CurrentPosition = newCurrentPosition;
            }

            return !IsCurrentBeforeFirst && !IsCurrentAfterLast;
        }

        public bool IsCurrentAfterLast
        {
            get { return CurrentPosition >= _itemCount; }
        }

        public bool IsCurrentBeforeFirst
        {
            get { return CurrentPosition < 0; }
        }

        public event CurrentChangingEventHandler CurrentChanging;

        protected void OnCurrentChanging(CurrentChangingEventArgs e)
        {
            CurrentChangingEventHandler handler = CurrentChanging;
            if (handler != null) handler(this, e);
        }

        public event EventHandler CurrentChanged;

        protected void OnCurrentChanged(EventArgs e)
        {
            EventHandler handler = CurrentChanged;
            if (handler != null) handler(this, e);
        }

        public IEnumerator<VirtualItem<T>> GetEnumerator()
        {
            for (int i = 0; i < _itemCount; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(VirtualItem<T> item)
        {
            return item.Parent == this;
        }

        public void CopyTo(VirtualItem<T>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return _itemCount; }
        }

        object ICollection.SyncRoot
        {
            get { throw new NotImplementedException(); }
        }

        bool ICollection.IsSynchronized
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        bool IList.IsFixedSize
        {
            get { throw new NotImplementedException(); }
        }

        #region Not Implemented IList methods


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

        int IList.Add(object value)
        {
            throw new NotImplementedException();
        }

        bool IList.Contains(object value)
        {
            if (value is VirtualItem<T>)
            {
                return Contains(value as VirtualItem<T>);
            }
            else
            {
                return false;
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        int IList.IndexOf(object value)
        {
            var virtualItem = value as VirtualItem<T>;
            if (virtualItem == null)
            {
                return -1;
            }
            else
            {
                return virtualItem.Index;
            }
        }

        void IList.Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        void IList.Remove(object value)
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
