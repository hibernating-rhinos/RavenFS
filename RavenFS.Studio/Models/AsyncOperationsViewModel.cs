using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class AsyncOperationsViewModel : Model
    {
        public AsyncOperationsViewModel()
        {
        }

        public AsyncOperationsModel Model { get { return ApplicationModel.Current.AsyncOperations; } }
    }
}