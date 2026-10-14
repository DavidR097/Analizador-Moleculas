using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace AnalizadorMoleculas.ViewModels
{
    public class VirtualizedObservableCollection<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private List<T> _master = new();

        private List<T> _view = new();

        private string _currentFilter = "";
        private readonly Func<T, string, bool> _filterPredicate;

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        
        public VirtualizedObservableCollection(Func<T, string, bool> filterPredicate = null)
        {
            _filterPredicate = filterPredicate ?? ((_, __) => true);
        }

        public int TotalCount => _master.Count;

        public int Count => _view.Count;

        public bool IsReadOnly => true;

        public void LoadItems(IEnumerable<T> items)
        {
            _master = (items as List<T>) ?? items?.ToList() ?? new List<T>();
            _currentFilter = "";
            _view = _master;
            RaiseReset();
        }


        public void ApplyFilter(string filter)
        {
            _currentFilter = filter ?? "";

            if (string.IsNullOrWhiteSpace(_currentFilter))
            {
                _view = _master;
            }
            else
            {
                _view = _master.Where(item => _filterPredicate(item, _currentFilter)).ToList();
            }

            RaiseReset();
        }

        public void Clear()
        {
            _master.Clear();
            _master = new List<T>();
            _view = _master;
            _currentFilter = "";
            RaiseReset();
        }


        public T this[int index]
        {
            get => _view[index];
            set => throw new NotSupportedException();
        }

        public int IndexOf(T item) => _view.IndexOf(item);
        public bool Contains(T item) => _view.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _view.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => _view.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _view.GetEnumerator();


        public void Add(T item) => throw new NotSupportedException();
        public void Insert(int i, T item) => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public void RemoveAt(int i) => throw new NotSupportedException();


        private void RaiseReset()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
