using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
            PendingOperationsCount = new Observable<int>();
            IsFault = new Observable<bool>();

            ClearCompletedOperationsAutomatically = new Observable<bool> {Value = true};
            ClearCompletedOperationsAutomatically.PropertyChanged += HandleClearCompletedOperationsAutomaticallyChanged;
        }

        private void HandleClearCompletedOperationsAutomaticallyChanged(object sender, PropertyChangedEventArgs e)
        {
	        if (ClearCompletedOperationsAutomatically.Value)
		        ClearOperations(o => o.Status == AsyncOperationStatus.Completed);
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
            UpdateStatusProperties();
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
            Debug.Assert(operation != null);

            if (e.PropertyName == "Status")
            {
                if (ClearCompletedOperationsAutomatically.Value
                    && operation.Status == AsyncOperationStatus.Completed)
                {
                    RemoveOperation(operation);
                }
                else if (operation.Status == AsyncOperationStatus.Error && !IsPaneVisible.Value)
                {
                    IsPaneVisible.Value = true;
                }

                UpdateStatusProperties();
            }
        }

        private void UpdateStatusProperties()
        {
            IsFault.Value = Operations.Any(o => o.Status == AsyncOperationStatus.Error);
            PendingOperationsCount.Value = Operations.Count(o => o.Status == AsyncOperationStatus.Queued || o.Status == AsyncOperationStatus.Processing);
        }

        public Observable<bool> IsFault { get; private set; }

        public Observable<int> PendingOperationsCount { get; private set; }

        public Observable<bool> IsPaneVisible { get; private set; }

        public Observable<bool> ClearCompletedOperationsAutomatically { get; private set; }
 
        public ReadOnlyObservableCollection<AsyncOperationModel> Operations { get { return operationsWrapper; } }

        public void Do(Func<Task> taskGenerator, string description)
        {
            var operation = new AsyncOperationModel() {Description = description};

            var task = taskGenerator();
            task.UpdateOperationWithOutcome(operation);

            RegisterOperation(operation);
        }
    }
}
