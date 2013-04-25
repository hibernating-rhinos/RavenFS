using System;

namespace RavenFS.Studio.Infrastructure
{
    public class ActionCommand : Command
    {
        private readonly Action<object> action;
        private readonly Func<object, bool> canExecute;

        public ActionCommand(Action<object> action) : this(action, _ => true)
        {
            
        }

        public ActionCommand(Action<object> action, Func<object, bool> canExecute)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public ActionCommand(Action action) : this(_ => action())
        {
        }

        public override void Execute(object parameter)
        {
            action(parameter);
        }

        public override bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }
    }
}
