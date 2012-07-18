using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Features.Search;
using RavenFS.Studio.Features.Search.ClauseBuilders;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchPageModel : PageModel
    {
        private readonly SearchResultsCollectionSource resultsSource;
        private IList<SearchClauseBuilderModel> searchClauseBuilders;
        private ICommand searchCommand;
        private ICommand clearSearchCommand;

        private ICommand downloadCommand;
        private ICommand deleteCommand;
        private ICommand editCommand;
        private ICommand navigateCommand;
        private ICommand renameFileCommand;
        private ICommand moveFileCommand;

        public ICommand RenameFile { get { return renameFileCommand ?? (renameFileCommand = new RenameFileCommand(SelectedItems)); } }
        public ICommand MoveFile { get { return moveFileCommand ?? (moveFileCommand = new MoveFileCommand(SelectedItems)); } }
        public ICommand OpenParentFolder { get { return navigateCommand ?? (navigateCommand = new NavigateToParentFolderCommand(SelectedItems)); } }
        public ICommand Download { get { return downloadCommand ?? (downloadCommand = new DownloadCommand(SelectedItems)); } }
        public ICommand Delete { get { return deleteCommand ?? (deleteCommand = new DeleteCommand(SelectedItems)); } }
        public ICommand EditProperties { get { return editCommand ?? (editCommand = new EditFilePropertiesCommand(SelectedItems)); } }

        public SearchPageModel()
        {
            resultsSource = new SearchResultsCollectionSource();
            resultsSource.SearchError += HandleSearchError;
            Results = new VirtualCollection<FileSystemModel>(resultsSource, 50, 10);
            Query = new Observable<string>();
            SelectedFile = new Observable<VirtualItem<FileSystemModel>>();
            SelectedItems = new ItemSelection<VirtualItem<FileSystemModel>>();
            SearchErrorMessage = new Observable<string>();
            IsErrorVisible = new Observable<bool>();

        }

        private void HandleSearchError(object sender, SearchErrorEventArgs e)
        {
            var exception = e.Exception;

            string message;
            if (exception is AggregateException)
            {
                var errorText = (exception as AggregateException).ExtractSingleInnerException().Message;
                errorText = TryExtractMessageFromJSONError(errorText);
                message = errorText;
            }
            else
            {
                message = exception.Message;
            }

            SearchErrorMessage.Value = message;
            IsErrorVisible.Value = true;
        }

        public void ClearSearchError()
        {
            SearchErrorMessage.Value = string.Empty;
            IsErrorVisible.Value = false;
        }

        private string TryExtractMessageFromJSONError(string errorText)
        {
            try
            {
                var jObject = JObject.Parse(errorText);
                return jObject["Message"] != null ? jObject["Message"].Value<string>() : errorText;
            }
            catch (Exception)
            {
                return errorText;
            }
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
        public Observable<VirtualItem<FileSystemModel>> SelectedFile { get; private set; }
        public ItemSelection<VirtualItem<FileSystemModel>> SelectedItems { get; private set; }

        public Observable<string> SearchErrorMessage { get; private set; }
        public Observable<bool> IsErrorVisible { get; private set; }

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
                                                        .Subscribe(_ => resultsSource.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));

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
            ClearSearchError();
            resultsSource.SearchPattern = Query.Value;
            ApplicationModel.Current.State.LastSearch = Query.Value;
        }
    }
}
