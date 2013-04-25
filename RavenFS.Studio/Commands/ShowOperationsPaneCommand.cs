using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class ShowOperationsPaneCommand : ActionCommand
    {
        public ShowOperationsPaneCommand() : base(() => ApplicationModel.Current.AsyncOperations.IsPaneVisible.Value = true)
        {
        }
    }
}
