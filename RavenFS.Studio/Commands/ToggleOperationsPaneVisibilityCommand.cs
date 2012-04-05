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
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class ToggleOperationsPaneVisibilityCommand : ActionCommand
    {
        public ToggleOperationsPaneVisibilityCommand()
            : base(() => ApplicationModel.Current.AsyncOperations.IsPaneVisible.Value = !ApplicationModel.Current.AsyncOperations.IsPaneVisible.Value)
        {
        }
    }
}
