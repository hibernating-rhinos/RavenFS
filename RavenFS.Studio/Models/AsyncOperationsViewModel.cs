using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class AsyncOperationsViewModel : ViewModel
    {
        public AsyncOperationsViewModel()
        {
        }

        public AsyncOperationsModel Model { get { return ApplicationModel.Current.AsyncOperations; } }
    }
}