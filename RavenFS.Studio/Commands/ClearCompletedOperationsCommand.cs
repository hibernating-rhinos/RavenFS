using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class ClearCompletedOperationsCommand : ActionCommand
    {
        public ClearCompletedOperationsCommand()
            : base(() => ApplicationModel.Current.AsyncOperations.ClearCompletedOperations())
        {
            
        }
    }
}
