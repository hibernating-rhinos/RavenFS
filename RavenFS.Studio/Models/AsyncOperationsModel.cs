using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class AsyncOperationsModel : NotifyPropertyChangedBase
    {
        private ReadOnlyObservableCollection<AsyncOperationModel> operationsWrapper;
        private ObservableCollection<AsyncOperationModel> operations;

        public AsyncOperationsModel()
        {
            operations = new ObservableCollection<AsyncOperationModel>();
            operationsWrapper = new ReadOnlyObservableCollection<AsyncOperationModel>(operations);

            operations.CollectionChanged += HandleOperationsChanged;

            IsPaneVisible = new Observable<bool>();

            ClearCompletedOperationsAutomatically = new Observable<bool> {Value = true};
            ClearCompletedOperationsAutomatically.PropertyChanged += HandleClearCompletedOperationsAutomaticallyChanged;
        }

        private void HandleClearCompletedOperationsAutomaticallyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ClearCompletedOperationsAutomatically.Value)
            {
                ClearOperations(o => o.Status == AsyncOperationStatus.Completed);
            }
        }

        private void ClearOperations(Func<AsyncOperationModel, bool> predicate)
        {
            var completedOperations = Operations.Where(predicate).ToList();
            foreach (var operation in completedOperations)
            {
                RemoveOperation(operation);
            }
        }

        private void HandleOperationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                IsPaneVisible.Value = true;
            }
        }

        private void RemoveOperation(AsyncOperationModel operation)
        {
            operation.PropertyChanged -= HandleOperationPropertyChanged;
            operations.Remove(operation);
        }

        public void RegisterOperation(AsyncOperationModel operation)
        {
            operations.Add(operation);
            operation.PropertyChanged += HandleOperationPropertyChanged;
        }

        public void ClearCompletedOperations()
        {
            ClearOperations(o => o.Status != AsyncOperationStatus.Queued && o.Status != AsyncOperationStatus.Processing);
        }

        private void HandleOperationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var operation = (sender as AsyncOperationModel);

            if (e.PropertyName == "Status" 
                && ClearCompletedOperationsAutomatically.Value
                && operation.Status == AsyncOperationStatus.Completed)
            {
                RemoveOperation(operation);
            }
        }

        public Observable<bool> IsPaneVisible { get; private set; }

        public Observable<bool> ClearCompletedOperationsAutomatically { get; private set; }
 
        public ReadOnlyObservableCollection<AsyncOperationModel> Operations { get { return operationsWrapper; } }
    }
}
