using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        }

        public void RegisterOperation(AsyncOperationModel operation)
        {
            operations.Add(operation);
        }

        public ReadOnlyObservableCollection<AsyncOperationModel> Operations { get { return operationsWrapper; } }
    }
}
