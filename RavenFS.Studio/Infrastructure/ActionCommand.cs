using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Infrastructure
{
    public class ActionCommand : Command
    {
        private readonly Action<object> action;

        public ActionCommand(Action<object> action)
        {
            this.action = action;
        }

        public ActionCommand(Action action) : this(_ => action())
        {
        }

        public override void Execute(object parameter)
        {
            action(parameter);
        }
    }
}
