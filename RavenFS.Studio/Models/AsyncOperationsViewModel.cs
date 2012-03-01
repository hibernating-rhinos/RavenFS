using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class AsyncOperationsViewModel : Model
    {
        private ICommand hidePaneCommand;

        public AsyncOperationsViewModel()
        {
            IsPaneVisible = new Observable<bool>();

            (ApplicationModel.Current.AsyncOperations.Operations as INotifyCollectionChanged).CollectionChanged +=
                HandleOperationsChanged;
        }

        private void HandleOperationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                IsPaneVisible.Value = true;
            }
        }

        public IList<AsyncOperationModel> Operations { get { return ApplicationModel.Current.AsyncOperations.Operations; } }

        public ICommand HidePaneCommand {get { return hidePaneCommand ?? (hidePaneCommand = new ActionCommand(() => IsPaneVisible.Value = false)); }}

        public Observable<bool> IsPaneVisible { get; private set; }

        public Observable<bool> ClearCompletedOperationsAutomatically { get { return ApplicationModel.Current.AsyncOperations.ClearCompletedOperationsAutomatically; } } 
    }
}