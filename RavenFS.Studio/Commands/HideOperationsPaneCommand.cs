using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class HideOperationsPaneCommand : ActionCommand
    {
        public HideOperationsPaneCommand() : base(() => ApplicationModel.Current.AsyncOperations.IsPaneVisible.Value = false)
        {
        }
    }
}
