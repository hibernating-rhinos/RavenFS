using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows.Input;
using RavenFS.Studio.Features.Search;
using RavenFS.Studio.Features.Search.ClauseBuilders;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchPageModel : PageModel
    {
        private readonly SearchResultsCollectionSource resultsSource;
        private ICommand searchCommand;
        private ICommand clearSearchCommand;
        private IList<SearchClauseBuilderModel> searchClauseBuilders;

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


        public IList<SearchClauseBuilderModel> SearchClauseBuilders
        {
            get { return searchClauseBuilders; }
            private set
            {
                searchClauseBuilders = value;
                OnPropertyChanged("SearchClauseBuilders");
            }
        }

        protected override void OnViewLoaded()
        {
            Query.Value = ApplicationModel.Current.State.LastSearch;
            HandleSearch();

            ApplicationModel.Current.Client.Notifications.FolderChanges("/")
                                                        .TakeUntil(Unloaded)
                                                        .Throttle(TimeSpan.FromSeconds(1))
                                                        .ObserveOn(DispatcherScheduler.Instance)
                                                        .Where(_ => !Query.Value.IsNullOrEmpty())
                                                        .Subscribe(_ => resultsSource.Refresh());

            CreateSearchClauseBuilders();
        }

        private void CreateSearchClauseBuilders()
        {
            var searchClauseBuilderType = typeof (SearchClauseBuilder);
            var builders = searchClauseBuilderType.Assembly
                .GetTypes()
                .Where(t => t.Namespace == searchClauseBuilderType.Namespace
                            && searchClauseBuilderType.IsAssignableFrom(t)
                            && !t.IsAbstract)
                .Select(t => Activator.CreateInstance(t) as SearchClauseBuilder)
                .ToList();

            var models = builders.Select(b => new SearchClauseBuilderModel(b, HandleBuilderAddSearchClause)).ToList();

            SearchClauseBuilders = models;
        }

        private void HandleBuilderAddSearchClause(string clause)
        {
            var query = Query.Value;

            if (query.IsNullOrEmpty())
            {
                Query.Value = clause;
            }
            else if (query.Contains(" OR "))
            {
                Query.Value = query + " OR " + clause;
            }
            else
            {
                Query.Value = query + " AND " + clause;
            }

            HandleSearch();
        }

        private void HandleSearch()
        {
            resultsSource.SearchPattern = Query.Value;
            ApplicationModel.Current.State.LastSearch = Query.Value;
        }
    }
}
