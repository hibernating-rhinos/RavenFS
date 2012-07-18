using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchResultsCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        public SearchResultsCollectionSource()
        {
            
        }

        public event EventHandler<SearchErrorEventArgs> SearchError;

        private string searchPattern;

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                if (searchPattern == value)
                {
                    return;
                }
                searchPattern = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }

        protected override Task<IList<FileSystemModel>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return DoQuery(start, pageSize, sortDescriptions)
                        .ContinueWith(t =>
                                          {
                                              var result = (IList<FileSystemModel>) ToFileSystemModels(t.Result.Files).Take(pageSize).ToArray();
                                              SetCount(t.Result.FileCount);
                                              return result;
                                          });
        }

        private Task<SearchResults> DoQuery(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.SearchAsync(searchPattern, MapSortDescription(sortDescriptions), start: start, pageSize: pageSize)
                .ContinueWith(t =>
                                  {
                                      if (t.IsFaulted)
                                      {
                                          OnSearchError(new SearchErrorEventArgs() { Exception = t.Exception});
                                      }

                                      return t.Result;
                                  });
        }

        private string[] MapSortDescription(IList<SortDescription> sortDescriptions)
        {
            if (sortDescriptions == null)
            {
                return new string[0];
            }

            var sortDescription = sortDescriptions.FirstOrDefault();

            FilesSortOptions sort = FilesSortOptions.Default;

            string sortField = "";

            if (sortDescription.PropertyName == "Name")
            {
                sortField = "__fileName";
            }
            else if (sortDescription.PropertyName == "Size")
            {
                sortField = "__size";
            }
            else if (sortDescription.PropertyName == "LastModified")
            {
                sortField = "__modified";
            }

            if (sortField.Length > 0 && sortDescription.Direction == ListSortDirection.Descending)
            {
                sortField = "-" + sortField;
            }

            if (!sortField.IsNullOrEmpty())
            {
                return new[] {sortField};
            }
            else
            {
                return new string[0];
            }
        }

        private static IEnumerable<FileSystemModel> ToFileSystemModels(IEnumerable<FileInfo> files)
        {
            return files
                .Where(fi => fi != null)
                .Select(fi => new FileModel
                                  {
                                      FormattedTotalSize = fi.HumaneTotalSize,
                                      FullPath = fi.Name,
                                      Metadata = fi.Metadata
                                  });
        }


        protected override Task<int> GetCount()
        {
            if (searchPattern.IsNullOrEmpty())
            {
                return TaskEx.FromResult(0);
            }
            else
            {
                return DoQuery(0, 0, null)
                    .ContinueWith(t => t.Result.FileCount, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        protected void OnSearchError(SearchErrorEventArgs e)
        {
            var handler = SearchError;
            if (handler != null) handler(this, e);
        }
    }

    public class SearchErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }
}
