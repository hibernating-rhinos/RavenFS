using System;
using System.Collections;
using System.Collections.Generic;

namespace RavenFS.Studio.Infrastructure
{
    public class LruList<T> : IEnumerable<T>
    {
        LinkedList<T> _list = new LinkedList<T>();
        private int _size;

        public LruList(int size)
        {
            _size = size;
        }

        public int Size
        {
            get { return _size; }
            set
            {
                if (_size < 1)
                {
                    throw new ArgumentException();
                }

                _size = value;

                TrimList();
            }
        }

        public void Store(T item)
        {
            _list.AddFirst(item);
            TrimList();
        }

        private void TrimList()
        {
            while (_list.Count > _size)
            {
                _list.RemoveLast();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
