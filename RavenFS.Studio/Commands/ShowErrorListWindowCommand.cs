using RavenFS.Studio.Features.Util;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class ShowErrorListWindowCommand : Command
    {
        public override void Execute(object parameter)
        {
            ErrorListWindow.ShowErrors(parameter as Notification);
        }
    }
}
