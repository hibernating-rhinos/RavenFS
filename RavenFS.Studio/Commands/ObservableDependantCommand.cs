using System.ComponentModel;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
    public abstract class ObservableDependantCommand<T> : Command where T:class
    {
        private readonly Observable<T> observable;
        private T currentValue;

        public ObservableDependantCommand(Observable<T> observable)
        {
            this.observable = observable;
            observable.PropertyChanged += HandleObservableValueChanged;
            CaptureCurrentValue();
        }

        protected T CurrentValue
        {
            get { return currentValue; }
        }

        private void HandleObservableValueChanged(object sender, PropertyChangedEventArgs e)
        {
            if (CurrentValue != null && CurrentValue is INotifyPropertyChanged)
            {
                (CurrentValue as INotifyPropertyChanged).PropertyChanged -= HandleInnerPropertyChanged;
            }

            CaptureCurrentValue();

            RaiseCanExecuteChanged();
        }

        private void CaptureCurrentValue()
        {
            currentValue = observable.Value;

            if (CurrentValue != null && CurrentValue is INotifyPropertyChanged)
            {
                (CurrentValue as INotifyPropertyChanged).PropertyChanged += HandleInnerPropertyChanged;
            }
        }

        private void HandleInnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseCanExecuteChanged();
        }
    }
}
