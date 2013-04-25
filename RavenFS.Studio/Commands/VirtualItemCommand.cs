using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
    public class VirtualItemCommand<T> : ObservableDependantCommand<VirtualItem<T>> where T : class
    {
        public VirtualItemCommand(Observable<VirtualItem<T>> observableItem) : base(observableItem)
        {
        }

        public sealed override bool CanExecute(object parameter)
        {
            return CurrentValue != null
                && CurrentValue.IsRealized
                && !CurrentValue.IsStale
                && CanExecuteOverride(CurrentValue.Item);
        }

        protected virtual bool CanExecuteOverride(T item)
        {
            return true;
        }

        public sealed override void Execute(object parameter)
        {
	        if (CanExecute(null))
		        ExecuteOverride(CurrentValue.Item);
        }

        protected virtual void ExecuteOverride(T item)
        {
            
        }
    }
}
