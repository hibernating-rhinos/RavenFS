using System;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Expression.Interactivity.Core;
using RavenFS.Studio.Infrastructure;
using ActionCommand = RavenFS.Studio.Infrastructure.ActionCommand;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchPageModel : PageModel
    {
        private readonly SearchResultsCollectionSource resultsSource;
        private ICommand searchCommand;
        private ICommand clearSearchCommand;

        public SearchPageModel()
        {
            resultsSource = new SearchResultsCollectionSource();
            Results = new VirtualCollection<FileSystemModel>(resultsSource, 50);
            Query = new Observable<string>();
        }

        public ICommand Search { get { return searchCommand ?? (searchCommand = new ActionCommand(HandleSearch)); } }

        public ICommand ClearSearch
        {
            get { return clearSearchCommand ?? (clearSearchCommand = new ActionCommand(() =>
                                                                                           {
                                                                                               Query.Value = "";
                                                                                               HandleSearch();
                                                                                           })); }
        }

        public Observable<string> Query { get; private set; }

        public VirtualCollection<FileSystemModel> Results { get; private set; }

        protected override void OnViewLoaded()
        {
            Query.Value = ApplicationModel.Current.LastSearch;
            HandleSearch();

            ApplicationModel.Current.Client.Notifications.FolderChanges("/")
                                                        .TakeUntil(Unloaded)
                                                        .Throttle(TimeSpan.FromSeconds(1))
                                                        .ObserveOn(DispatcherScheduler.Instance)
                                                        .Where(_ => !Query.Value.IsNullOrEmpty())
                                                        .Subscribe(_ => resultsSource.Refresh());
        }

        private void HandleSearch()
        {
            resultsSource.SearchPattern = Query.Value;
            ApplicationModel.Current.LastSearch = Query.Value;
        }
    }
}
