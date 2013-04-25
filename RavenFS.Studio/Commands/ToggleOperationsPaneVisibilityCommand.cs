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
