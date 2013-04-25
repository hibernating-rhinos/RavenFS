using System;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
    public class ActionObservableDependantCommand<T> : ObservableDependantCommand<T> where T:class
    {
        private readonly Action<T> execute;
        private readonly Func<T, bool> canExecute;

        public ActionObservableDependantCommand(Observable<T> observable, Action<T> execute) : base(observable)
        {
            this.execute = execute;
        }

        public ActionObservableDependantCommand(Observable<T> observable, Action<T> execute, Func<T, bool> canExecute)
            : base(observable)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public override void Execute(object parameter)
        {
            execute(CurrentValue);
        }

        public override bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(CurrentValue);
        }
    }
}
