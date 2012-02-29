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
        private readonly Action _action;

        public ActionCommand(Action action)
        {
            _action = action;
        }

        public override void Execute(object parameter)
        {
            _action();
        }
    }
}
